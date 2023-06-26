# What is the DLCS?

First of all you need to know what an Image Server is.

[IIPImage](https://iipimage.sourceforge.io/), [Loris](https://github.com/loris-imageserver/loris) and [Cantaloupe](https://cantaloupe-project.github.io/) are Image Servers.

For a given high-resolution master image (say a 100 megapixel tiff or JPEG2000) an image server is able to generate a jpeg of the whole image or a region of the image, at different requested sizes.

One major use case for this is _Deep Zoom_ - you could never view a 100 Megapixel tiff in the browser, but your image viewing client can make many _tile_ requests to fill your current viewport with small JPEGs appropriate to the region of the image you are looking at, and the current zoom level.

## Examples

### A thumbnail

![https://dlcs.io/iiif-img/wellcome/5/b22031194_0009.jp2/full/122,200/0/default.jpg](https://dlcs.io/iiif-img/wellcome/5/b22031194_0009.jp2/full/122,200/0/default.jpg)

```
                                                      region  size  r quality format
                                                      \    / /    \   |     | |  /
https://dlcs.io/iiif-img/wellcome/5/b22031194_0009.jp2/full/122,200/0/default.jpg
```

### A bigger picture 300 pixels wide

![https://dlcs.io/iiif-img/wellcome/5/b22031194_0009.jp2/full/300,/0/default.jpg](https://dlcs.io/iiif-img/wellcome/5/b22031194_0009.jp2/full/300,/0/default.jpg)

```
                                                      region size  quality format
                                                      \    //   |  |     | |  /
https://dlcs.io/iiif-img/wellcome/5/b22031194_0009.jp2/full/300,/0/default.jpg
```

### A 256 x 256 tile requested by a deep zoom viewer

![https://dlcs.io/iiif-img/wellcome/5/b22031194_0009.jp2/768,2048,256,256/256,/0/default.jpg](https://dlcs.io/iiif-img/wellcome/5/b22031194_0009.jp2/768,2048,256,256/256,/0/default.jpg)

```
                                                        ____region____  size   quality format
                                                       /              \ |   |  |     | |  /
https://dlcs.io/iiif-img/wellcome/5/b22031194_0009.jp2/768,2048,256,256/256,/0/default.jpg
```

## Image servers need to be FAST

The client makes many requests for tiles, possibly hundreds or even thousands as the user pans and zooms around the image. This would be very slow and compute-intensive with a naive implementation - open the 100MB hi-res image, crop, resize, convert to JPEG, serve the tile; do this hundreds of times a second to deal with real world load.

While most image servers can deal with many standard image formats, performance is dramatically improved if the source images are JPEG2000 or Pyramidal TIFF. These formats allow progressive access. They encode multiple resolution levels. You don't need to open the whole bitmap but instead can randomly access the part of the file you need to generate the current tile.

We use JPEG2000. There are two JPEG2000 libraries we use for decoding: Kakadu, which is commercial, and OpenJPEG which isn't. On Wellcome we use the IIPImage Server compiled with Kakadu, and on other projects we use IIPImage compiled with OpenJPEG. Kakadu is very fast. OpenJPEG is catching up, but still has a way to go.

Given a disk volume full of JPEG2000s, and an IIPImage server with that volume mounted, you can make requests like those above and it will open the right file and generate the derivative requested. It needs a file system, rather than (say) AWS S3, because it needs to make random access reads of the JPEG2000.

## So why the DLCS?

We could just have a cluster of one or more IIPImage Servers, a sensible file naming scheme, and lots of disk space accessible by those servers.

The problem with that is scale. Wellcome has a lot of images - ~ 40 million and counting, dozens of terabytes of disk space. They are already storing them in S3, for digital preservation.

Access to these images also follows a [steep long-tail distribution](https://github.com/dlcs/protagonist/issues/47). Many images won't get looked at for months or even years, whereas some are looked at all the time.

We want the best of both worlds - we want the low cost of S3 storage for terabytes of images, especially as many of them are rarely used. But we _need_ the performance and crucially the random access file system behaviour of EBS and similar volumes.

The DLCS is an attempt to have this cake and eat it.

The DLCS uses IIPImage under the hood. But it fronts it with an _Orchestrator_ that copies files from S3 _origins_ to a local volume for use by IIPImage. As far as IIPImage is concerned, whenever it gets a request to extract a region from an image, that image is where it expects it to be on a locally readable disk. But it's only there because the orchestrator ensured it was there before the request arrived at IIPImage.

At any one time, only a very small subset of the possible images that the DLCS knows about (that have been registered) are on expensive fast disks. This _cache_ is what the DLCS maintains - ensuring images are present when IIPimage needs them, and scavenging disk space to keep this working set at a sensible size.

The rest of the images that the DLCS knows about are in S3. Sometimes this S3 will be the DLCS's own bucket, because it copied the image there from its original origin when the image was registered, or because it created a JPEG2000 derivative from the original origin version so it could serve tiles faster.

But sometimes, it will be the customer's S3 bucket - if the image is already a tile-optimised JPEG2000, and if the DLCS knows it can just fetch it from there whenever it needs to Orchestrate in the future, then no additional storage is required - the DLCS treats customer origins like this as if it were its own storage. This usually requires some granting of cross-AWS account access, so the DLCS can read the customer's bucket(s).

## We need more than this MVP

The description above is the core of what the DLCS does. But we need more! It gets far more complex when it's a managed service that needs to support other functionality, and integrate via its API with customer digitisation workflows, and for Digirati to use on our projects in lots of different ways. It needs to be a managed service, with admin UI, APIs and other features. And there are some issues with the simple orchestration scenario described above.


### Thumbnails

While access to images for deep zoom is an extreme long tail distribution, some scenarios throw a spanner in this model. Suppose we have a 1000-page book that nobody has looked at for years. None of its images are in the "hot" cache. Suddenly someone looks at it in a viewer that makes hundreds of thumbnail requests for the pages to generate its UI.

Even if the user doesn't actually look at all those pages, we've had to _Orchestrate_ - move hundreds of images to the hot cache, just to generate one thumbnail for each and nothing else. That's a bit wasteful.

To help avoid this the DLCS makes a set of thumbnails for each image when it is registered and stores them in S3. When it gets image requests for full region images that match a thumbnail size, it proxies the image response directly from S3 without troubling the image server, which means no orchestration was required - the master image was not copied from S3 just to generate a thumbnail.

### Access Control

Not everything is freely visible to anonymous users. The DLCS needs to enforce access control, on behalf of the customer, on the image pixels it serves. Some images are OK for public use up to a certain size (e.g., you can see a thumbnail but not a higher resolution version). Others require specific permissions.

When an image is registered via the API it can be given a required _Role_ - the user must be in this role to see the image. The DLCS then has an API for "backchannel" use for acquiring the roles for a given user, so it can establish a session and then match the user's known roles with the roles demanded by the image.

The DLCS does this for the client by implementing the IIIF Auth API, and for role acquisition a simple CAS-like delegation to some services the customer must provide (e.g., at Wellcome we implement this part in the DDS).

### A comprehensive API

How do images get into the DLCS? How do you register origins and manage millions of images? How do you set policies, such as the sizes of thumbnails to produce, or configuration of access control?

In some scenarios, the DLCS might be the back end to a content creation tool, like a manifest editor. Users build IIIF resources and register new assets with the DLCS.

More commonly, the DLCS is used in a complex systems integration context. It's part of a digitisation workflow. As high resolution master images roll off of the digitisation production line, they are registered with the DLCS to provide public access via the Image API, or AV derivatives. To fit into many different scenarios, the DLCS has an API that allows external workflows, dashboards and other tools to integrate with it.


### Metadata for development scenarios

The most important thing to tell the DLCS about an image is its origin - where to find the master image.
But images have other metadata - some intrinsic, like height or width; and some arbitrary, supplied by the customer. Roles and tags can be supplied, and also three string fields and three integer fields that the customer can use for any purpose. This is very useful for building workflows, synchronisation and reconciliation - you can use the DLCS to store some values with the images for whatever programmatic purpose you might want later.

### Named queries

One of these purposes is _named queries_ - you can ask the DLCS to select from images where one of the values matches something, then order by one of the other values. The DLCS can return this query as a IIIF Manifest. So while it doesn't know anything about structure above the level of an individual image, you can use the DLCS to construct skeleton Manifests based on queries, because you used your knowledge of higher level structure and organisation to give the DLCS additional metadata that you can later query on.

### Audio, Video

It's not just images.

You can register audio and video with the DLCS. It could be a multi-gigabyte unoptimised archival video file, completely inappropriate for web use. This is analogous to our 100 megapixel image above. The DLCS will convert the origin image into one or more web-friendly derivatives (e.g., MP3 for audio) and store them. It uses Amazon Elastic Transcoder to generate the web-friendly versions.

This _Asset Delivery_ is key to understanding the point of the DLCS - you have one system or source of not-web-friendly archive images, AV files etc. And the DLCS provides access to them through open standards - the IIIF Image API for images, and web-friendly AV formats for time-based media. It's not a preservation system itself - it provides highly scalable, high performing access to these digital assets, for web use.

### PDFs, Word Documents and other files

Some of the things our customers want to serve up at scale, and maybe enforce access control on, don't actually require any transformation for web access. For example a PDF. In these cases, we don't need image servers or Elastic Transcoder - just store the file. The DLCS can do this too.

### PDF generation

Example: https://dlcs.io/pdf/wellcome/pdf/5/b22031194

This combines the named query functionality with the large thumbnail generation, to create PDFs from sequences of images.
It knows how to request a title page, whether to redact any pages based on permissions, and whether to enforce access control on the PDF document.

## Diagrams

(To be moved from dlcs-io-ops)
