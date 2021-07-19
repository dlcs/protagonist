# Special Server

## Overview

Images for `/full` requests are likely to be the user lookign at one image at a time.

By handling these requests differently from 'normal' image requests (see [Orchestrator](002-storage-and-orchestration.md)) we can serve them more efficiently by not orchestrating but make a byte range, or complete requests, to the source S3 asset. We save orchestration space and CPU cycles for tile requests, interactions that are triggered by deep zoom. Full region requests for requests for images.

We could store the JPEG2000 header as data, separately, so we don't need to go to S3 to read it. We have it handy so that if someone asks for a smallish size, but full region, we can read just enough to service it from the source JP2 with a byte range request. 

Someone asking for `/full/max/` can be made to wait longer than someone asking for a readable static image (perhaps the view before deep-zooming) or tiles, these kinds of user interactions are very different.

This discussion is related: https://groups.google.com/d/msg/iiif-discuss/OOkBKT8P3Y4/u2Lah-h_EAAJ

Serving tiles via small byte range requests to S3 still seems like a lot of work, I'd like the image-server to be dealing with as fast a file system as possible, as directly as possible, for handling tile requests. But we could end up where every other kind of `/full/` request is either handled by proxying a ready-made derivative in S3, or by on-the-fly image processing of a stream from S3.

Obviously, sensible reverse-proxy caching is important here too.