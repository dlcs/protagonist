# Storage and Orchestration


## Context

From [What is the DLCS?](../what-is-dlcs-io.md)

> We could just have a cluster of one or more Image Servers and lots of disk space accessible by those servers.

> The problem with that is scale. For example, Wellcome has a lot of images: 40 million and counting, dozens of terabytes of disk space. They are already storing them in S3, for digital preservation.

> Access to these images also follows a steep long-tail distribution. Many images won't get looked at for months or even years, whereas some are looked at all the time.

> We want the best of both worlds - we want the low cost of S3 storage for terabytes of images, especially as many of them are rarely used. But we _need_ the performance and crucially the random access filesystem behaviour of EBS and similar volumes.

> The DLCS is an attempt to have this cake and eat it.

> The DLCS uses IIPImage under the hood (although it could use other image servers). But it fronts it with an _Orchestrator_ that copies files from S3 _origins_ to a local volume for use by IIPImage. As far as IIPImage is concerned, whenever it gets a request to extract a region from an image, that image is where it expects it to be on a locally readable disk. But it's only there because the orchestrator ensured it was there before the request arrived at IIPImage.

> At any one time, only a very small subset of the possible images that the DLCS knows about (that have been registred) are on expensive fast disks. This _cache_ is what the DLCS maintains - ensuring images are present when IIPimage needs them, and scavenging disk space to keep this working set at a sensible size.

## Architecture

The _Orchestrator_ application is responsible for fetching assets from their Origin so that they are ready to be used by the ImageServer(s). In addition to this it can 

![Architecture](img/orchestrator-arch.png)

The above diagram has many components, these are discussed below but a short overview of them:

### Orchestrator

At a very simple level the Orchestrator is a reverse proxy that contains a _lot_ more business logic than would normally be found in a reverse proxy. It makes decisions on incoming requests and decides where these should be routed. It is a collection of containerised services that act together to serve image-assets.

#### Reverse Proxy

This is a standard Reverse Proxy. It could sit in front of the Orchestrator application (for example [Lua script in NGINX](https://github.com/openresty/lua-nginx-module#readme)) and make back-channel requests 'into' the main Orchestrator application to aid in decision making.

Alternatively it could be inside the Orchestrator application (for example [YARP](https://microsoft.github.io/reverse-proxy/) for a dotnet application) and use in-process requests to aid in decision making.

The type of information that can inform decision making is:

* Is the request for an asset that requires auth?
* Is the request for a full image we have a thumbnail for?
* Is the request for a `/full/` image?
* Is the request for a tile that we have served recently (and is in cache)?

#### Cache

The cache inside the Orchestrator is a disk-based (e.g. [Varnish](https://varnish-cache.org/)) or in-process memory cache (or a combination of the two). This is a hot-cache of recently generated tiles and can be used to serve future requests for these. The exact caching algorithm used (e.g. LRU vs LFU) will depend on implementation.

#### Orchestrator

The main business logic behind Orchestration. This maintains a list of what images are on the the Fileshare. If a request comes in for an image that is _not_ on the file share then it will copy the tile-optimised image from Storage to Fileshare, ready to be used by the image-servers.

#### Fireball

Fireball is an implementation of a PDF generator. When a request is received for a generated PDF, Fireball can create and store the PDF from a manifest.

### Distributed Lock

This is an external resource that can provide distributed locking for multiple Orchestrator instances. It is used to avoid multiple Orchestration requests. For example, when someone opens an image in a IIIF Viewer and a flood of 40 tile requests come in simultaneously. In this instance we only want to Orchestrate (copy the image from source to Fileshare) once, rather than 40 times. The Distributed Lock helds to 'hold back' 39 of the requests until the image is available for use by the image-servers.

### Cache

The external cache sits between the Image Server Cluster and Orchestrator instances. Whether it is required depends on individual use cases but it can help:

* Speed on 'on boarding' of a new Orchestrator instance when scaling up.
* Cache authorised tiles - the auth check will have been carried out at Orchestrator layer.

### Fileshare

Fast, local network attached storage. This contains the source tile-optimised files that the image-servers will use to generate tiles from. It is ignorant of how to fetch these files, the Orchestrator is responsible for fetching the files from storage and putting them onto the Fileshare before triggering requests to the image-server.

### Image Server Cluster

1:n Image-Servers that are used for generation of assets to be served over the



- copy from origin
- serve tiles
- clean up
- use info.json as poke to start
- use distributed lock to handle 30 requests for same image

## Orchestration

The DLCS uses local EBS volumes for IIPImage to read, and the request pipeline holds up an image request if orchestration is required. It uses Redis and separate scavenger processes to maintain knowledge of what's where, and to keep the disk usage of this _hot cache_ stable.

But this is still our extra plumbing, more complexity to manage. 

One ideal scenario is an imaginary AWS offering - S3-backed volumes where you can specify a source bucket, and the size of "real" volume you want (e.g., 1TB). The file system view can be read only - we don't need to write to the bucket via a filesystem, we can do that as S3. Reads of the filesystem manage the orchestration at that file access, below our application logic. We just assume that if it's in the bucket, it can be read from the file system, and everything looks simple.

This is _like_ S3fs-fuse, but a managed service. https://github.com/s3fs-fuse/s3fs-fuse

Other people use S3fs-fuse with IIPImage, but either with customisations - https://github.com/klokantech/embedr/issues/16 - or interventions for cache management that are similar to what we're already doing, so not particularly easier to manage.

Other approaches we looked at 5 years ago were commercial offerings on top of AWS, like SoftNAS, but they didn't have quite the features we wanted.

There is an echo of AWS EFS in this. We tried using EFS rather than EBS for IIPImage, but found it too slow. A write operation is not finished until everything is consistent, and this was just too slow for image orchestration.

## Alternatives to Orchestration where possible

We're already offering the separate `/thumbs/` path for cases where the client knows what sizes to ask for.

We can take this further.

There's a difference between image requests for the `/full/` region, and tile requests. Full region requests (that don't match a thumbnail size) are likely to be the user looking at one image at a time, whereas tile requests arrive in a flood, for the same image, and are generated by deep zoom clients.

We could direct `/full/` requests down a different path, that doesn't orchestrate but makes byte range, or complete requests, to the source S3 JP2. We save orchestration space for tile requests, interactions that are triggered by deep zoom. Full region requests for small

We could even store the JP2 header as data, separately, so we don't need to go to S3 to read it. We have it handy so that if someone asks for a smallish size, but full region, we can read just enough to service it from the source JP2 with a byte range request. Someone asking for `/full/max/` can be made to wait longer than someone asking for a readable static image (perhaps the view before deep-zooming) or tiles, the kinds of user interactions are very different.

This discussion is related: https://groups.google.com/d/msg/iiif-discuss/OOkBKT8P3Y4/u2Lah-h_EAAJ

Serving tiles via small byte range requests to S3 still seems like a lot of work, I'd like IIPImage (or whatever we are using) to be dealing with as fast a file system as possible, as directly as possible, for handling tile requests. But we could end up where every other kind of `/full/` request is either handled by proxying a ready-made derivative in S3, or by on-the-fly image processing of a stream from S3.

Obviously, sensible reverse-proxy caching is important here too.

## Other things to look at

### AWS File Gateway

On the face of it, AWS Storage Gateway looks a lot like the hypothetical service described earlier: https://aws.amazon.com/storagegateway/file/

The File Gateway can be run on EC2.

However, there are some issues that would limit us:

> An object that needs to be accessed by using a file share should only be managed by the gateway. If you directly overwrite or update an object previously written by file gateway, it results in undefined behavior when the object is accessed through the file share.

This would preclude use cases where the DLCS makes use of the existing buckets of an [archival storage system](https://github.com/wellcomecollection/docs/blob/extract-docs/rfcs/002-archival_storage/README.md); we'd need to copy images into another S3 bucket, which means synchronisation issues as well as huge amounts of extra storage.
 
This could be an option for some scenarios though, and we could do some performance testing on it.

### Azure Data Lake Storage

https://docs.microsoft.com/en-gb/azure/storage/blobs/data-lake-storage-introduction

> Azure Data Lake Storage Gen2 is a set of capabilities dedicated to big data analytics, built on Azure Blob storage. Data Lake Storage Gen2 is the result of converging the capabilities of our two existing storage services, Azure Blob storage and Azure Data Lake Storage Gen1. Features from Azure Data Lake Storage Gen1, such as file system semantics, directory, and file level security and scale are combined with low-cost, tiered storage, high availability/disaster recovery capabilities from Azure Blob storage.

## Next steps

What are we missing here? What other ways of doing this are there? Is the system we've got actually the best way of doing it (with some modifications)?

Sources of concern:

A flood of tile requests for the same image can't all trigger orchestration of that image. We make it the equivalent of a critical section, we use a semaphore. While this is as light as possible, it still seems wasteful. Or at least, I'd rather it was someone else's problem.

How well do the mentioned solutions handle multiple concurrent demands for the same file?

What's the most efficient way to optimise this? Avoiding multiple orchestration attempts, but recognising that all the request are independent? We use Redis and some Lua code in NGINX. 
