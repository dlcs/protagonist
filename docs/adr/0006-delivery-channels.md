# Delivery Channels

For Wellcome born digital work we introduced the concept of delivery channel - a flexible way of specifying what transformations (if any) should be applied to an asset. An asset (such as an image or a PDF) may have multiple delivery channels. A JPEG2000 could have the `iiif-img` delivery channel _and_ the `thumbs` delivery channel _and_ the `file` delivery channel, meaning "make a level-2 IIIF Image Service available", "make preset thumbs available as a level-0 image service", "serve the original image file as-is". 

The Wellcome implementation is a halfway house. The proposed DLCS API for a full implementation is described on this page of the API docs:

https://github.com/dlcs/docs/blob/main/pages/api-doc/delivery-channels.mdx

([current draft](https://github.com/dlcs/docs/blob/wip-skeleton/pages/api-doc/delivery-channels.mdx))

Everything on a delivery channel is always served by the same application / service / component. Even if this means there is more than one delivery channel providing IIIF Image Services, or PDFs. It is not the format but the processing and transformation that determines the channel. An example of this is further developed in [ADR 0007](0007-delivery-channels-and-thumbs.md) 


Future delivery channels could be things like:

 - `clips` for video snippets
 - `iiif-img-from-video` for arbitrary level 2 image services from arbitrary time points
 - `av-derivatives` for dynamic creation of new derivatives (give me this video from 10s to 30s at 720p as an avi) - ffmpeg-as-a-service
 - `iiif-img-from-pdf` for arbitrary level 2 image services from arbitrary pdf pages

... as well as adding more supported content types to existing channels.
