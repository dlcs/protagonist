# Special Server Implementation

Last update: 2023-06-08

## Context

At a high level there are 4 possible downstream services that Orchestrator will proxy to when handling an image request:

* if region=full, quality=default, format=jpg, rotation=0 and size={known thumb}, proxy to `thumbs` service. Else,
* if region=full, quality=default, format=jpg, rotation=0 and size={within threshold of known thumb}, proxy to `thumbs-resize` service. Else,
* if region=full, proxy to `special-server`. Else,
* proxy to `cantaloupe`

> For simplicity the above logic is for open images only

`special-server` and `cantaloupe` are 2 different configurations of the same image.

Compared to the functionality of "normal" Cantaloupe, `special-server` has narrow feature requirements:

* read a file directly from S3
* return the full image only (because region=full) as a web friendly derivative, in accordance with IIIF image request.

This RFC looks at whether we should use an alternative approach for `special-server` - using a custom dotnet application for simplicity.

## dotnet Implementation

We have carried out a PoC, implementing a basic `special-server` purely in dotnet managed code. This is a basic dotnet7 web application with 2 main external dependencies:

* [Jpeg2000.Net](https://bitmiracle.com/jpeg2000/) (j2knet). A licensed library for decoding jp2s for futher processing.
* [ImageSharp](https://sixlabors.com/products/imagesharp/). Powerful open source image manipulation library, already used in `thumbs-resize` component (can have [licensing fees](https://sixlabors.com/pricing/) depending on usage).

The test implementation has a single API endpoint with format:
* `/iiif/{s3-source}/{iiif-image-request}`, (e.g. `iiif/s3:%2F%2Ftest-bucket%2Ffoo.jpg/full/1000,/0/default.jpg`) where:
  * `s3-source` is the `s3://` URL with `/` encoded as `%2F`
  * `iiif-image-request` is a normal IIIF image request. As this is a PoC, only a subset of properties are supported:
    * `size` - only `/full/`, `/w,h/`, `/w,/` and `/,h/`
    * `rotation` - mirror not supported
    * `quality` - only default (color) and grayscale
    * `format` - only `jpg` and `png`

The URL format matches how orchestrator calls special-server now, simplifying testing.

### Implementation Notes

No jp2s were larger than ~8MB, handling larger images of 100MB+ could yield different results.

The time spent downloading the entire file from S3 was marginal.

The majority of processing time is spent in jp2ks `image.Decode()` call. This can be avoided by saving the generated TIFF to disk on first request. This gave considerable performance improvements on subsequent calls but would need a scavenger service to cleanup over time.

jp2k library requires a seekable stream. `SeekableS3Stream` from https://github.com/mlhpdx/seekable-s3-stream was used for this purpose. Copying the full file to `MemoryStream` could be an option if the file is small enough. In general the seekable stream implementation was faster. 

Memory/CPU usage was not monitored during testing, the only metric used was response time for full HTTP response to be fulfilled. We would need to implement load testing to accurately test resource requirements.

Similarly, accuracy of generated images was not tested (colour accuracy, sharpness etc).

The bitmaps generated from j2knet were not compatible with ImageSharp. When opening via ImageSharp the error `"ImageSharp does not support this BMP file. File header bitmap type marker '8'."` was thrown. Due to this TIFF was used. When saved to disk the TIFF was marginally smaller.

jp2k was noticably slower without setting `ResolutionLevelsToDiscard` to a value > 0. Higher values are much quicker but result in a much smaller image being returned, this could be leveraged for smaller size requests.

### Outstanding Questions

The PoC was [IIIF ImageApi Level 0 compliant](https://iiif.io/api/image/3.0/compliance/) - would that be enough? If not:
* How much effort would be involved in handling full image-request parameters? 
* Would some mutations be more performant in an alternative library from ImageSharp? 
* Are all mutations possible in ImageSharp?

jp2k is _not_ open source so may not be available for all users of DLCS - is this a factor?

With the CustomerOriginStrategy `use-original`, customers could register a non-jp2 image as image-server source (e.g. a small JPEG). We would need to somehow detect the file type and handle accordingly - presumably sending the file directly to ImageSharp. Ideally we do not want logic in Orchestrator to proxy/not-proxy to `special-server` depending on image-type, as this is unnecessary coupling between `special-server` implementation and Orchestrator.

As noted above `ResolutionLevelsToDiscard` has a large affect on performance - how would we determine which value to use? Based on source image-size and image-request?

`SeekableS3Stream` could possibly be made more efficient by using different page length + count values. We would likely need a process of reading first X bytes of file to determine optimal handling of each file.

## Comparison

For comparison both configurations were deployed as ECS Fargate containers with 2048/5120 (vCPU/Memory).

The Cantaloupe configuration used was v4 with Kakadu 7. The instance(s) used were Wellcome's production instances (so there may have been additional traffic).

For all tests the dotnet PoC was run using `ResolutionLevelsToDiscard=1` and `SeekableS3Stream`.

| Request                   | Desc      | Cantaloupe | Dotnet |
| ------------------------- | --------- | ---------- | ------ |
| `/full/max/0/default.jpg` | proquest  | 8780       | 4720   |
| `/1200,/0/default.jpg`    | proquest  | 1629       | 4680   |
| `/200,200/0/default.jpg`  | proquest  | 379        | 4930   |
| `/1000,/0/default.png`    | proquest  | 3830       | 6720   |
| `/1000,/90/default.jpg`   | proquest  | 1857       | 4470   |
| `/full/max/0/default.jpg` | ia        | 3850       | 3300   |
| `/1200,/0/default.jpg`    | ia        | 722        | 1684   |
| `/200,200/0/default.jpg`  | ia        | 296        | 1320   |
| `/1000,/0/default.png`    | ia        | 2460       | 6380   |
| `/1000,/90/default.jpg`   | ia        | 1330       | 2490   |
| `/full/max/0/default.jpg` | ia cached | 1419       | 512    |
| `/1200,/0/default.jpg`    | ia cached | 295        | 335    |
| `/200,200/0/default.jpg`  | ia cached | 62         | 126    |
| `/1000,/0/default.png`    | ia cached | 1075       | 3520   |
| `/1000,/90/default.jpg`   | ia cached | 240        | 280    |

> Comparison figures were against Cantaloupe w/ Kakadu only - if revisiting it would be worthwhile testing Cantaloupe w/ Grok and OpenJPEG.

## Overall

Overall I don't think it would be worth writing a custom component to replace the current Cantaloupe `special-server`.

The main reasons for this are:
* Performance is noticeably slower than Cantaloupe (when using Kakadu).
* Cantaloupe is configurable enough out of the box for what we need `special-server` to do.
* Cantaloupe has features that could open up possibilities for `special-server` without us needing to write any custom code.
* Cantaloupe is widely used, however it has not been maintained of late and this is something we should keep an eye on.
* Assuming we don't encounter any issues or want to do anything bespoke customising Cantaloupe should be config changes only, rather than coding changes.
* As we encounter different sizes and types of file we may need to tweak the dotnet code to accommodate.
* Config of `ResolutionLevelsToDiscard` and `SeekableS3Stream` could take a lot of trial and error to get correct.
* In general the dotnet implementation was slower, to get better performance we need to save the file to disk which comes with additional disk-space management.

However, the benefits of a dotnet application are:
* We have run into some issues with Cantaloupe using [Grok/OpenJPEG](https://github.com/dlcs/image-server-node-cantaloupe/issues/7) and [Kakadu 8.2.1](https://github.com/dlcs/image-server-node-cantaloupe/issues/6) so we may revisit this, or get familiar with internals of Cantaloupe to help resolve issues, in the future.
* Simplicity, there are a lot less moving parts. Troubleshooting issues would be simpler. We are in control of full release cycle of fixes.
* Having a single `special-server` setup for all customers is an attractive proposition, no disparity between deployments.

## Useful Links

* [Cantaloupe Image Server](007-cantaloupe-image-server.md#full-requests)
* https://bitmiracle.com/jpeg2000/
* https://github.com/dlcs/image-server-node-cantaloupe
* https://github.com/dlcs/protagonist/issues/517