# Special Server Implementation

Last update: 2023-06-08

## Context

At a high level there are 4 possible downstream services that Orchestrator will proxy to when handling an image request:

* if region=full, quality=default, format=jpg, rotation=0 and size={known thumb}, proxy to `thumbs` service. Else,
* if region=full, quality=default, format=jpg, rotation=0 and size={within threshold of known thumb}, proxy to `thumbs-resize` service. Else,
* if region=full, proxy to `special-server`. Else,
* proxy to `cantaloupe`

> for simplicity the above is for open images only

`special-server` and `cantaloupe` are 2 different configurations of the same image.

Compared to the functionality of "normal" Cantaloupe, `special-server` has narrow feature requirements:

* read a file directly from S3
* return the full image (as region=full) as a web friendly derivative, in accordance with IIIF image request.

This RFC looks at whether we should use an alternative approach for `special-server` - using a custom dotnet application for simplicity.

## dotnet Implementation

We have carried out a PoC, implmementing a basic `special-server` implementation purely in dotnet managed code. This is a basic dotnet7 web application with 2 main external dependencies:

* [Jpeg2000.Net](https://bitmiracle.com/jpeg2000/). This is a licensed code that allows us to decode jp2's.
* [ImageSharp](https://sixlabors.com/products/imagesharp/). Very powerful open source image manipulation library, already used in `thumbs-resize` component (can have [licensing fees](https://sixlabors.com/pricing/) depending on usage).

The test implementation was a single API endpoint with format:
* `/iiif/{s3-source}/{iiif-image-request}`, (e.g. `iiif/s3:%2F%2Ftest-bucket%2Ffoo.jpg/full/1000,/0/default.jpg`) where:
  * `s3-source` is the `s3://` URL with `/` encoded as `%2F`
  * `iiif-image-request` is a IIIF image request, only a subset of properties were supported for test:
    * `size` - only `full` and `w,h`
    * `rotation` - mirror not supported
    * `quality` - only default (color) and grayscale 
    * `format` - only `jpg` and `png`

This matches how orchestrator calls special-server now, simplifying testing.

jp2k library requires a seekable stream. `SeekableS3Stream` from https://github.com/mlhpdx/seekable-s3-stream was used for this purpose.

### Implementation Notes

The time spent downloading the entire file from S3 was marginal. However, no jp2s were larger than ~8MB.

Majority of time is spent in jp2ks `image.Decode()` call. This can be avoided by saving the generated TIFF to disk (via `?serveMethod=cache`) - this made for considerable performance improvements on subsequent calls but would need a scavenger service to cleanup over time.

jp2k library needs a seekable stream for reading, copying the full file to `MemoryStream` could be an option if the file is small enough. In general the seekable stream implementation was faster. This could be made more efficient by using different page length + count values.

With the CustomerOriginStrategy `use-original`, customers could register a non-jp2 image as image-server source (e.g. a small JPEG). We would need to somehow detect the file type and handle accordingly - presumably sending the file directly to ImageSharp.

The bitmaps generated from j2k were not compatible with ImageSharp. When opening via ImageSharp the error `"ImageSharp does not support this BMP file. File header bitmap type marker '8'."` was thrown. Due to this TIFF was used. When saved to disk the TIFF was marginally smaller.

How much effort would be involved in handling full image-request parameters? Would some mutations be more performant in an alternative library from ImageSharp? Are all mutations possible in ImageSharp?

Hard to compare memory + cpu usage without load testing.

jp2k is _not_ open-source so may not be available for all users - is this a factor?

jp2k was noticably slower without setting `ResolutionLevelsToDiscard` to a value > 0. Higher values are much quicker but result in a much smaller image being returned, this could be leveraged for smaller sizes. However how do we know what size to use?

## Comparison

For comparison both configurations were deployed as ECS Fargate containers with 2048/5120 (vCPU/Memory). SpecialServer was the instance in use by Wellcome.

The Cantaloupe configuration used was v4 with Kakadu 7.

For all tests dotnet was using `?discard=1` and `SeekableStream`.

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

## Overall

Overall I don't think it would be worth writing a custom component to replace `special-server`.

The main reasons for this are:
* Cantaloupe is configurable enough out of the box for what we need `special-server` to do.
* It has features that could open up possibilities for `special-server` without us needing to write any custom code.
* Widely used, however it has not been maintained of late and this is something we should keep an eye on.
* As we encounter different sizes and types of file we may need to tweak the code.
* Config of `ResolutionLevelsToDiscard` and `SeekableS3Stream` could take a lot of trial and error to get correct.
* In general the dotnet implementation was slower, to get better performance we need to save the file to disk which comes with additional disk-space management.

However, we have run into some issues with Cantaloupe using [Grok/OpenJPEG](https://github.com/dlcs/image-server-node-cantaloupe/issues/7) and [Kakadu 8.2.1](https://github.com/dlcs/image-server-node-cantaloupe/issues/6) so we may revisit this, or get familiar with internals of Cantaloupe to help resolve issues, in the future.

## Further Reading

* [Cantaloupe Image Server](007-cantaloupe-image-server.md#full-requests)
* https://bitmiracle.com/jpeg2000/
* https://github.com/dlcs/image-server-node-cantaloupe