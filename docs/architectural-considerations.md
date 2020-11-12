## Architectural considerations for asset delivery

How does the DLCS deal with the identified [Interaction Patterns](https://github.com/dlcs/protagonist/issues?q=is%3Aissue+label%3A%22Interaction+Pattern%22+sort%3Acreated-asc)?


### Assumptions

We’re going to assume that, for all _image_ assets, deep zoom is available. That doesn’t mean that all user interfaces will always incorporate a pan and zoom interaction, just that some will, at least some of the time. This means that our IIIF Image Services should support tiles.

We’re going to assume that there are a lot of images. Hundreds of millions, perhaps, when all the sources of images are added up. They may be spread across hundreds of different tenants - customers and spaces within customers.

Some of these images are very high resolution, high quality digitization of artworks. The original files are very large. The ability to see fine detail is important for some interactions, and reuse of images (tiles, crops).

Some of these images are lower resolution images of digitised pages, often of printed books. They tend to have smaller original files. This is partly because the things being imaged are often physically smaller, but also because the level of detail required is less.

An entire Art Museum may only generate the same number of image _files_ as a few feet of shelf of digitised library. But those files are usually much bigger - and have very different interaction patterns and usage characteristics.

At least some images will be available for download - that is, users can download a reasonably high resolution version of the full image.


### Different levels of Image API	

The IIIF Image API is designed to support _[ramping up](https://iiif.io/api/annex/notes/design_patterns/#intelligently-manage-ramping-up)_ - you can start with static files on disk and offer some functionality, and progress to dynamic image serving, offering more functionality. To deliver a service you must at least provide an image information document, the JSON file that describes the services you are offering for a given image (the info.json). You must also serve an image response to all the URLs that _could_ be requested, given the information in the info.json document.

The simplest possible IIIF Image API implementation is just a single static JPEG image file, as long it is served from a URL that conforms to the specification and is accompanied by an info.json document that describes the (in this case, minimal) service. Only one API request (a URL with parameters) is possible, and it will return the single image file. 

A more useful service would offer the same full image at various sizes, listed in the info.json. Client code can choose the size most appropriate for their needs, whether it is a thumbnail, a larger image, or even a [responsive image tag](https://tomcrane.github.io/iiif-img-tag/).

Neither of the above give you true, efficient deep zoom. For this, [you need tiles](https://www.gasi.ch/blog/inside-deep-zoom-1). We can still do this with static tiles on disk, we just need a lot of them, and a script to process the master image to generate these static tile images. 

A large source image will require potentially thousands of tile images on disk (or in inexpensive storage that can be given a web front end, like Amazon S3 or Azure Blob Storage). Although see the discussion of tile size, later. 

All of the above are _Level 0_ IIIF Image Services - they do not need a dynamic image server, like Loris or IIPImage. They are cheap, and scalable, and will usually provide high performance deep zoom without huge amounts of infrastructure costs.

**_What does an image server give us that a static tile pyramid does not?_**

With an image server, we can request arbitrary regions, at arbitrary sizes, in different formats, rotations and mirrored. This opens up a much larger world of creative reuse. With a static tile set, we can only request the image tiles that have been created as a one-off operation, by a script. These are unlikely to be useful as images in their own right, only when consumed by a deep zoom client such as OpenSeadragon or Leaflet, which manages the seamless stitching together. A dynamic image server is a much more powerful pair of **_[digital scissors](https://www.youtube.com/watch?v=-od9S8kn5b8)_**.

With an image server, derivatives are created on the fly. There is no need to store thousands of small tile images, just the single master image that the image server will generate derivatives from at run-time, in response to user requests. This can be significant - mass digitisation of books will create many billions of tiles, many of which may never be used. There is a tradeoff - store one JPEG2000 file, but require an image server to respond to Image API requests, or store thousands of JPEG tiles, but require only static file hosting. 

An image server makes it easier to change our minds later. A better image server might come along that we can use with the same source images. We might change our minds about tile sizes for deep zoom, and supported formats. We can continue to offer new features.

Static image pyramids can be more resilient to heavy load, as they just require a web server. A dynamic image server is more susceptible to being swamped with load from concurrent requests.

Combinations of these approaches are possible. Static, pre-made images for some types of request, dynamic tiles for others.


## The basic requirement

**_An asset delivery platform that, if you tell it the URI of an image (regardless of format), will provide a IIIF Image API service endpoint for that image, the particular features of which can be controlled by policies._**

This URI is the **origin** of the image. It could be any web address - provide an image service for https://example.org/mona-lisa.tiff. In practice it is likely to be the address of a master image file, delivered from a preservation system or some other more private location. It might not be an HTTP address, it could be an Amazon S3 location, or an FTP location, or some other protocol. There is one essential ingredient in providing the Image Service for this image - the Image Server.


### The Image Server

IIPImage, Loris and Cantaloupe are Image Servers.

For a given high-resolution master image (say a 100 megapixel tiff or JPEG2000) an image server is able to generate a derivative of the whole image or a region of the image, at different requested sizes. Usually the derivative is a jpeg but Image Servers usually generate other formats too, such as PNG.


#### Image servers need to be FAST

The client makes many requests for tiles, possibly hundreds or even thousands as the user pans and zooms around the image. This would be very slow and compute-intensive with a naive implementation - open the 100MB hi-res image, crop, resize, convert to JPEG, serve the tile; do this hundreds of times a second to deal with real world load.

While most image servers can deal with many standard image formats, performance is dramatically improved if the source images are JPEG2000 or Pyramidal TIFF. These formats allow progressive access. They encode multiple resolution levels. You don't need to open the whole bitmap but instead can randomly access the part of the file you need to generate the current tile.

If using JPEG2000, there are two libraries available for decoding: Kakadu, which is commercial, and OpenJPEG, which isn't. You can use the IIPImage Server compiled with either Kakadu or OpenJPEG. Kakadu is very fast. OpenJPEG is catching up, but still has a way to go.

Given a disk volume full of JPEG2000s, and an IIPImage server with that volume mounted, you can make requests like those above and it will open the right file and generate the derivative requested. **It needs a filesystem, rather than (say) an objectstore like AWS S3, because it needs to make random access reads of the JPEG2000.**


![alt_text](images/image1.png "simple image server")


This is the simplest kind of setup. 

Buy enough fast disk storage to hold all your images, mount that disk storage on an image server instance, and expose it to the web.

This doesn’t address:

*   How do the images get to be on that fast disk?
*   How do you manage them?
*   What if they are not in a suitable tiled format for the image server to use them?
*   What if some of the images require access control?
*   What if they need different policies for maximum download size, and/or other properties?
*   Fast disks start to get very expensive for millions of large image files
*   What about AV?
*   What about other renderings of sets of images (e.g., a PDF)?
*   How does it scale up for large volumes of traffic?

The last point is conceptually simplest to address. You start to have multiple image servers, in a load balanced cluster, adding as many as needed to meet peak load requirements, and you can add a cache in front of the image server, to store the most commonly requested derivatives:


![alt_text](images/image2.png "clustered image server with cache")


This addresses performance, but not any of the other issues.

The problem with the lower part of the image is the scale of the content itself. A large institution has a lot of images - hundreds of millions, potentially many terabytes of disk space. These are likely already stored in a digtal preservation system.

Access to these images is likely to follow a [steep long-tail distribution](https://github.com/dlcs/protagonist/issues/47). Many images won't get looked at for months or even years, but some are looked at all the time. It’s not cost effective to store hundreds of millions of seldom-accessed images on high cost SSDs in anticipation of the moment they are needed to service an image request. Better to use lower cost object storage such as AWS S3 or Azure Blob Storage. Especially if you are already storing them for preservation.

We want the best of both worlds - we want the low cost of blob storage for terabytes of images, especially as many of them are rarely used. But we need the performance and crucially the random access file system behaviour of EBS and similar volumes, because this is what Image Servers need (with some exceptions discussed later).

The answer to this is an architecture that involves _Orchestration_. This means bringing images to the fast disk storage as-and-when they are required, and only when required. The DLCS architecture is designed to do two seemingly contradictory things:

* Support orchestration as quickly and easily as possible
* Avoid orchestration wherever possible

The latter point needs some cooperation from client applications using the resources; the DLCS helps encourage them to make the right choice of request to avoid orchestration wherever possible.

The DLCS uses an image server, or a cluster of load-balanced image servers, under the hood. But it fronts the image servers with an _Orchestrator_ that copies files from low-cost _origins _(for example, S3) to a local filesystem volume for use by the image server. As far as the image server is concerned, whenever it gets a request to extract a region from an image, that image is where it expects it to be: on a locally readable disk. But it's only there because the orchestrator ensured it was there before the request arrived at the image server.

At any one time, only a small subset of the possible images that the DLCS knows about (that have been registered) are on expensive fast disks. This **“hot” cache** is what the DLCS maintains - ensuring images are present when the image server needs them, and scavenging disk space to keep this working set at a sensible size.

The rest of the images that the DLCS knows about are in cheaper storage, for example, AWS S3. Sometimes this S3 will be the system’s own bucket, because it copied the image there from its original origin when the image was registered, or because it **created a JPEG2000 derivative** from the original origin version (e.g. a TIFF) so it could serve tiles faster.

However, sometimes it will be some other component’s S3 bucket. If the image is already a tile-optimised JPEG2000 or pyramidal TIFF, and if the DLCS knows it can just fetch it from there whenever it needs to Orchestrate in the future, then no additional storage is required - the system treats such origins as if they were its own storage. This usually requires granting cross-AWS account access, so the system can read the customer's bucket(s).


![alt_text](images/image3.png "Image server with orchestrator")


The problem with orchestration is that it introduces a step that can hurt performance.

Any incoming request needs to be checked to make sure that the source image needed to service it is present - is orchestrated. This check has to impose as little overhead as possible. And if it isn’t present, the orchestration - typically a copy from S3 to EBS (on AWS) - has to happen. And any requests that depend on the same image that arrive while that is going on need to wait - and not also trigger an orchestration operation.

Typically, tile requests arrive in floods. However, they are usually preceded by a request for the info.json (from a client like OpenSeadragon) and we can use that as a trigger for orchestration. Almost all of the time, a single info.json request will be followed by a flood of tile requests.

This isn’t always true, however, and we need to cater for many concurrent tile requests for the same image arriving, when we have not been forewarned with the info.json request.

The Orchestrator needs to maintain a lock, and know the state of the image - is it already orchestrated, not orchestrated, or in the process of being orchestrated right now?

The performance and implementation of this lock is critical to the DLCS.


## Beyond the basic architecture

The description above is the core of what the DLCS is, for actually serving the images. But how do the images get there in the first place? 

We need more! It gets far more complex when it's a managed service that needs to support other functionality and integrate, via its API, with customer digitisation workflows, and for customers to use on diverse projects with very different use cases. It needs to be a managed service, with administrative user interface, instrumentation, reporting, and other features. And there are some issues with the simple orchestration scenario described above.


### Thumbnails

While access to images for deep zoom is an extreme long tail distribution, some scenarios throw a spanner into this model. Suppose we have a 1000-page book that nobody has looked at for years. None of its images are in the "hot" cache. Suddenly someone looks at it in a viewer that makes hundreds of thumbnail requests for the pages to generate its UI.

Even if the user doesn't actually look at all those pages, we've had to _Orchestrate_ - move hundreds of images to the hot cache, just to generate one thumbnail for each and nothing else. That's a bit wasteful.

To help avoid this the DLCS makes a set of thumbnails for each image when it is registered and stores them in blob storage (S3). The sizes of thumbnails to make are set by one or more thumbnail policies. When it gets image requests for full region images that match a thumbnail size, it proxies the image response directly from S3 without troubling the image server, which means no orchestration was required - the master image was not copied from S3 just to generate a thumbnail.

More on thumbnails:

[https://gist.github.com/tomcrane/093c6281d74b3bc8f59d](https://gist.github.com/tomcrane/093c6281d74b3bc8f59d)

Note that a variation of this thumbnail selection algorithm is present in Mirador 3. This means Mirador 3 is already optimised for the DLCS.


### Access Control

Not everything is freely visible to anonymous users. The DLCS needs to enforce access control, on behalf of the customer, on the image pixels it serves. Some images are OK for public use up to a certain size (e.g., you can see a thumbnail but not a higher resolution version). Others require specific permissions.

When an image is registered via the API it can be given a required _Role_ - the user must be in this role to see the image. The system then has an API for "backchannel" use for acquiring the roles for a given user, so it can establish a session and then match the user's known roles with the roles demanded by the image.

The DLCS does this for the client by implementing the IIIF Auth API, and for role acquisition a simple CAS-like delegation to some services the customer must provide (e.g., at Wellcome we implement this part in the DDS, their equivalent of the IIIF Server).


### A comprehensive API

How do images get into the DLCS? How do you register origins and manage millions of images? How do you set policies, such as the sizes of thumbnails to produce, or configuration of access control?

In some scenarios, the DLCS might be the back end to a content creation tool, like a manifest editor or Image Media Manager. Users build IIIF resources and register new assets with the system.

More commonly, the DLCS is used in a complex systems integration context. It's part of a digitisation workflow. As high resolution master images roll off of the digitisation production line, they are registered with the system to provide public access via the Image API, or AV derivatives. To fit into many different scenarios, the DLCS has an API that allows external workflows, dashboards and other tools to integrate with it.

[dlcs-book.readthedocs.io](https://dlcs-book.readthedocs.io/en/latest/)


### Client and Server API access

A Management API for systems integration is likely to be consumed from other servers - machine-to-machine. Some of the same API is likely to be consumed from browser-based web applications. Content creation tools will use HTTP calls to interact with the asset delivery platform API, including sending it new images. 

The platform needs to provide an API and access control policy for it that works for machine-to-machine and browser-to-server scenarios.

NB this is not the same as access control for the assets themselves, via IIIF Auth.

(DLCS Note - the DLCS API currently works well for machine-to-machine scenarios, but is not suited to direct browser-to-machine scenarios requiring authentication. This is an area we have identified for improvement, and alignment with other systems that use [JSON Web Tokens](https://jwt.io/)).


### Conversion to tile-ready format

We don’t want to put the onus of preparing an image file that is optimised for tile generation on the end user of the system, or on the systems that consume its API. In fact, this should be opaque to the API consumer, or users of tools that consume the API - they are just telling the system that they want it to provide an Image Service for a given image at some origin. The DLCS should decide whether the image is already appropriate for tile generation (e.g., it is a JPEG2000 with appropriate settings) and if not, convert the source image itself, and store that converted source image and use it from an internal origin, rather than the original.

In the real world, sense dictates that we allow some light into this process. We can be specific and set a policy that our source images are already **optimised** if they come from a certain location, so we can avoid preparing what we think are tile-ready JPEG 2000 images only for the system to convert them to slightly different ones.

We might also decide that the image is small enough not to bother converting it to a tile delivery format. If the tile size is 1024 and the source image is a 2000 x 2000 px high quality JPG, there are only 4 tiles that could be extracted, and the speed at which these could be generated from a JPG, while _probably_ slower than from a JP2, might not justify the creation of the JP2 in the first place.

Treating the origin as opaque, and the format used by the DLCS to drive image servers as internal private information, allows the implementation to change in future without changing external contracts. For example, if a more efficient image encoding comes along.


### Metadata for development scenarios

The most important thing to tell the DLCS about an image is its origin - where to find the master image. 

But images have other metadata - some intrinsic, like height or width; and some arbitrary, supplied by the workflow process, tool or other application that’s making use of the system. 

Roles and tags can be supplied, and also arbitrary string fields and integer fields that the caller can use for any purpose. This is very useful for building workflows, synchronisation and reconciliation - you can use the system to store some values with the images for whatever programmatic purpose you might want later.

**Using metadata for querying, harvesting and other system-to-system functions**

The DLCS allows the storing of arbitrary metadata per image. The system doesn’t understand what this metadata is, but it can query on and group by this metadata: \
 \
“List all the images where field1=zzz and field2=yyy, order by field3”

This capability allows for other systems to use the DLCS as a development platform. It can be used to run reports, harvest images that match a certain criteria, even possibly batch-update fields.


### Named queries

One of these purposes is _named queries_ - you can ask the DLCS to select from images where one of the values matches some criterion, then order by one of the other values. The system can return this query as a IIIF Manifest, and possibly other _projections_. So while it doesn't know anything about structure above the level of an individual image, you can use the system to construct skeleton Manifests based on queries, because you used your knowledge of higher level structure and organisation to give the DLCS additional metadata that you can later query on.


### Audio, Video

It's not just images.

You can register audio and video with the DLCS. It could be a multi-gigabyte unoptimised archival video file, completely inappropriate for web use. This is analogous to our 100 megapixel image above. The system will convert the origin image into one or more web-friendly derivatives (e.g., MP3 for audio) and store them. In current implementations it uses Amazon Elastic Transcoder to generate the web-friendly versions, which it stores in S3 for efficient delivery.

This _Asset Delivery_ is key to understanding the point of the DLCS - you have one system or source of not-web-friendly archive images, AV files etc. And the Model provides access to them through open standards - the IIIF Image API for images, and web-friendly AV formats for time-based media. It's not a preservation system itself - it provides highly scalable, high performing access to these digital assets, for web use.


### PDF generation

Example: [https://dlcs.io/pdf/wellcome/pdf-item/b22031194/0](https://dlcs.io/pdf/wellcome/pdf-item/b22031194/0) 

This combines the named query functionality with the large thumbnail generation, to create PDFs from sequences of images.

It knows how to request a title page, whether to redact any pages based on permissions, and whether to enforce access control on the PDF document.


### Dealing with full image requests

Thumbnails are one type of full image request. Others are downloads of high resolution derivatives, or even people harvesting images at the highest resolution they can. The latter scenario is another one likely to work against the caching advantage that long-tailed distribution gives us. If someone harvests a huge number of full images at sizes larger than the largest thumbnail, they will all need orchestrating to service these requests. This puts pressure on the cache. There are always going to be one-off requests that invoke an orchestration, this is part of the design, but we want to guard against scenarios that could fill the “hot” cache with images that are only there to serve one person.

The user experience of a large full image is different from that of image tiles. While users can expect tile delivery to be very fast, and the deep zoom experience smooth and free of glitches and artifacts, we can keep people waiting a little longer to download a large full image. It is also easier to rate-limit requests from the same source for full images, as opposed to tiles. 

This means we could choose to orchestrate full images in a different way - perhaps to a different orchestration location with a different size and eviction policies - or choose not to orchestrate them at all, and stream the necessary information directly from blob storage. The Cantaloupe image server has an implementation of byte-range request streaming from S3. This is substantially slower than an image server reading the file from a local disk, and would be very inefficient and slow for dealing with tiles - but could be appropriate for large full image requests. This means that small full images could be served using thumbnail derivatives made at ingest time, large full images could be served by processing image data directly from blob storage, leaving image servers and the orchestration process to meet traffic generated by deep zoom tile requests, and arbitrary region requests.


### What Image Server to use?

Our current implementation of the DLCS uses IIPImage. 

[Work by The Getty Research Institute](https://drive.google.com/drive/folders/10Fb-yT35D5fKmp34tyvQBO-U-3FphD11) concluded that IIPImage with pyramidal TIFFs is the fastest image delivery mechanism available, but raw speed isn’t the only consideration. 

For JPEG2000, IIPImage with Kakadu is faster than IIPImage with OpenJPEG - but the licensing costs for using IIPImage with Kakadu might be very high; that money could be spent on additional compute resources for OpenJPEG instead, or something else later.

If the user interfaces that most material is viewed through makes use of the thumbnail features described above, and large full images are handled by different means, and there is a large reverse proxy tile cache sitting in front of the asset delivery infrastructure, then the contribution that raw image server performance makes to the overall user experience and perception of performance is much less than if the image server alone is doing all the work.


![alt_text](images/image4.png "image_tooltip")


_Mirador 3 uses the thumbnail service offered by the DLCS - none of the thumbnails in this view would have triggered an orchestration event. None of them was served from an Image Server in the conventional sense._

Arbitrary region selection, such as might be produced in a cropping tool or in the UV’s “Save Current View” feature, is also an interaction where the perception of speed is not as important as with tile delivery. 

It’s important that **all** image delivery is fast - we don’t mean that people should be prepared to wait seconds - but that some patterns of image delivery must be as fast as possible, and others can be slightly slower, without any adverse effect on the user’s perception of the speed of the platform as a whole, the general performance of their interactions with a complex digital object. There are always tradeoffs to be made between cost and performance; the DLCS is about reducing cost without reducing perceived performance to the end user.

Deep Zoom tile delivery _for uncached tiles_ is the most important speed-critical function left to the image server, and here we can weigh up the characteristics of various image servers. 

IIPImage always offers the same size tile regardless of the encoding of a JPEG2000, whereas Loris and Cantaloupe are more observant of how the JPEG2000 was actually encoded, and give larger tiles if they are more efficient. IIPImage’s 256 x 256 tile size has been like that since it began, and might be too small - although you can edit the default[^6]. If using IIPImage, a 512 x 512 tile size might be a better default for the larger screens and higher bandwidth available in 2020. 

A tile size of 256 made sense 20 years ago, but now we have retina displays, faster internet connections, bigger monitors. The balance between the number of HTTP requests, size of individual requests, proportion of the viewport that such a request would tile: these are all different in 2020.

* Tiling your 800 x 600 viewport over a 56.K modem
* Tiling your retina display over fibre broadband

These are VERY different environmental factors for tile-size choice.

Our feeling is that a 512 tile size would feel faster, because it would mean:


 - 1/4 the number of HTTP requests (important for mobile)

 - That size image would no longer be considered "big"

 - 1/4 the per-request decoding overhead

That is, the same computational resources on the server would deliver a perceived user experience improvement, with this change.

And these arguments could be extended to 1024 pixel tile sizes (used by Harvard Art Museums). However, we haven’t gathered quantitative data on this yet.

[This painting](https://www.harvardartmuseums.org/collections/object/5240), with a 1024 pixel tile size, can only yield 6 possible tiles (6 HTTP requests). ([info.json](https://ids.lib.harvard.edu/ids/iiif/18204967/info.json))

Loris is generally the first to have a complete implementation of any new IIIF Image API features.

Cantaloupe has many intriguing extensions that might be useful (e.g., offering a IIIF Image service for stills from a video file). Some of these extensions would require that the DLCS was aware specifically of Cantaloupe, rather than treating the Image Server as a black box implementation of the Image API.

Ultimately, there is no reason why the DLCS couldn’t use more than one image server, and decide which one to use based on the request. E.g., route tiles to one cluster, arbitrary regions to another. Or route handling of small lossy source files (like digitised library book pages) to one cluster and large high quality artworks to another. The routing within the DLCS can make decisions based on the request parameters and the characteristics of the source image.

More configration options could tell the DLCS whether to convert to a tile-friendly format, what tile size to use, and so on.
Users of the DLCS also need to consider the effects of other consumers of their Image API resources. Even if you optimise your user interactions for perceptual speed, using all the DLCS' tricks, someone could be using your IIIF resources in an unoptimised way somewhere else, yielding poorer perceptual speed as well as increased load.

You can control this with options too - e.g., only offer [fixed sizes](https://tomcrane.github.io/scratch/osd/iiif-sizes.html).


### Orchestration Scavenging Algorithms

As images are moved to the fast disks by the Orchestrator, that finite disk space fills up.

Another process runs to **_scavenge_** the disk space, to ensure we never run out of room.

The current DLCS uses a simple _Least Recently Used _(LRU) approach. This could be made more complex, to take other factors into account when deciding what images to evict from the cache. These factors might include:


*   The number of times this has been accessed - e.g., although it hasn’t been used for a week, it regularly gets bursts of high traffic, so we predict that we’d only have to bring it back into the cache again soon
*   File size (and therefore, cost/time of orchestration) is a factor that works both ways. If we make larger images more sticky in the cache, we do avoid having to copy them around quite as much - but we fill our cache with fewer large images instead of many small ones, which may not be the desired outcome.
*   Images that should NEVER be orchestrated. This is a possible scenario that the architecture could support. Clients would need to assert this at registration time, and these images should live on their own volume, and get copied to that volume at registration time - if that volume is out of disk space, then there will be an error at registration time - and you could extend the disk space. See Appendix for an extended discussion of this.


### Support Image API 2.1 and 3

The DLCS can support more than one version of the Image API (and can even do this if the underlying Image Server can’t).

While not present in our current implementation, the architecture could support servicing the same origin image from two endpoints, one for each version of the API (and avoid orchestrating the same image just because it’s getting requests via both APIs).

There is very little performance overhead with this, but care needs to be taken in considering the Image Service URIs; the current DLCS design does not include service versioning in the path.


### Analytics

The DLCS currently collects logs, but nothing specialised for analytics. It could provide more detailed analytics. For large tiled images, it could even give insight into which parts of images people are looking at, zooming in to.


### Appendix - _The Night Watch_ as Orchestration Use Case

In principle, a DLCS would handle an image like the Night Watch just fine. IIPImage can handle gigapixel JPEG2000 and Pyramidal TIFFs.

The difficulty is orchestration - we assume that moving a few MB or even 10-20MB of JP2 from bob storage (S3) to random access disk (EBS) is an operation that won't inconvenience users, especially as, with a long tail distribution, most users do not experience it for most requests.

For the Night Watch, you would want this image permanently orchestrated, so that the tilesource image never had to be copied on demand to service a IIIF request.

As one of the treasures of the collection, The Night Watch would certainly be regularly accessed and therefore in the “hot” disk cache. And as long as it’s there, IIPImage (and other image servers) do not have a problem with images like this, as JP2s or Pyramidal TIFFs. The files are huge, but the image server never needs to load them whole, just bits of them. 

Consider, though, the case of very large images that are not as popular as the Night Watch. They might be aiding a conservation project. A large number of very large images would swamp the hot cache.

Cantaloupe has done some work in making [byte-range requests to JP2s in S3](https://groups.google.com/forum/#!msg/iiif-discuss/OOkBKT8P3Y4/u2Lah-h_EAAJ).

e.g., say if I had a max policy of 2000 px on that Night Watch image in the system.

If I ask for /full/max, [I could service that](https://github.com/dlcs/protagonist/blob/master/docs/rfcs/002-storage-and%20orchestration.md#alternatives-to-orchestration-where-possible) with maybe a 1~2 MB byte range request to S3, directly. Even if the JP2 in S3 is 100GB.

