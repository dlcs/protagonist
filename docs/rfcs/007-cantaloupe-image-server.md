# Cantaloupe Image Server

This RFC addresses the possibility of replacing the image-server used for the DLCS. As of January 2022 [IIPImage](https://iipimage.sourceforge.io/) is the image-server of choice, with [Cantaloupe](https://cantaloupe-project.github.io) being considered as a replacement. 

Cantaloupe offers a plethora of configuration options and that could lead to alternative handling of orchestration and image-serving.

> A note on terminology: `orchestrator` is a dotnet reverse-proxy service and "orchestration", or to "orchestrate", is the act of copying a file from A to B, ready for fulfilling a request.

## IIPImage Docker Images

For a point of reference, there are 2 Docker images that are used for DLCS deployments.

The [iipsrv-openjpeg](https://github.com/dlcs/image-server-node-iipsrv-openjpeg) Dockerfile builds [OpenJPEG](https://www.openjpeg.org/) 2.4.0 and includes it in the final Docker image.

The [iipsrv-kakadu](https://github.com/dlcs/image-server-node-docker) Dockerfile entrypoint is a shell script that downloads, extracts and uses [Kakadu](https://kakadusoftware.com/) compiled binaries from a specified S3 location.

## IIIF Support

| Image Server | Image API 2.x                                                                                                                     | Image API 3.0                                             |
|--------------|-----------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------|
| IIPImage     | Full level 1 + level 2 compliance with the exception of PNG export for [Image API 2.0](https://iiif.io/api/image/2.0/compliance/) | not-supported                                             |
| Cantaloupe   | Processor dependant, most are [level 2] for [Image API 2.1](https://iiif.io/api/image/2.1/compliance/)                            | [Full level 2](https://iiif.io/api/image/3.0/compliance/) |

Cantaloupe supports multiple different versions of the IIIF Image API, available on different paths (`/iiif/2/` and `/iiif/3/`).

`Orchestrator` supports generating IIIF 2.1 and 3.0 manifests (for [single item manifests](https://github.com/dlcs/protagonist/issues/183) and [named queries](https://github.com/dlcs/protagonist/issues/175)) on different paths as well as a canonical path that will return either v2.1 or v3.0 depending on configuration. 

This same path structure and configuration could be used to ensure that the ImageAPI matches the PresentationAPI version.

## Optimisations

The `orchestrator` is ultimately a glorified reverse-proxy. It parses incoming IIIF Image requests and attempts to shortcut the image-serving by using various optimised services like `thumbs` or the `special-server`.

The request is only handed off to the image-server if it cannot be served by other, more optimised, means. Before handing off, the source image (e.g. JPEG2000) needs to be copied from "slow" object storage to "fast" local storage (ie _orchestrated_). This adds some complexity to the `orchestrator` (holding up requests, managing disk space, monitoring which files are orchestrated etc) so we endeavour to do it as seldom as possible.

Cantaloupe has configurable [sources](https://cantaloupe-project.github.io/manual/5.0/sources.html) and [caches](https://cantaloupe-project.github.io/manual/5.0/caching.html) that specify where source images are to be found, and how they are to be cached.

These properties are of interest to the DLCS as it could allow for multiple, different flavours of image-server. We envisage there being 2 configurations initially, these are outlined more below:

* Cantaloupe instance(s) for `/full/` requests that don't have a matching thumbnail.
* Cantaloupe cluster for serving tiles requests.

### `/full/` Requests

When a `/full/` image request is received we will try to use a pre-generated thumbnail from S3 if it is for a matching size.

If we don't have a pre-generated thumbnail, but the request is within a configurable threshold of a size we _do_ have - then we can resize an existing thumbnail on the fly (e.g. we have a `/!200,200/` thumbnail and the request is for `/!180,180/`).

Both of these approaches avoid orchestrating an image. Now we can add fallback handling for `/full/` requests to a specially configured instance(s) of Cantaloupe. 

This would use an [`S3Source`](https://cantaloupe-project.github.io/manual/5.0/sources.html#S3Source) as this avoids the need to orchestrate the image. We plan to use OpenJPEG for decoding JPEG2000 image sources, however it doesn't [support streaming](https://cantaloupe-project.github.io/manual/5.0/processors.html#Supported%20Features) (therefore it can't stream the result directly from S3).

Finding the exact configuration to use will involve some testing as we'll need to identify which combination of processor and [`RetrievalStrategy`](https://cantaloupe-project.github.io/manual/5.0/processors.html#Retrieval%20Strategies) to use. e.g.

* Use a "download" or "cache" retrieval strategy (these differ in that the former deletes the source file immediately after serving the request, whereas the latter caches it) with OpenJPEG.
* Use a less performant processor that supports streaming (e.g. [Java2D](https://cantaloupe-project.github.io/manual/5.0/processors.html#Java2dProcessor) which uses native-Java processing).
* Use Kakadu, which supports streaming but has a high licensing cost.

### Tile Requests

It is expected that tile requests will continue to be handled by the DLCS "orchestrating" the source image and handing the request off to a cluster of Cantaloupe servers using a [`FileSystemSource`](https://cantaloupe-project.github.io/manual/5.0/sources.html#FilesystemSource) and OpenJPEG. This is what currently happens with IIPImage.

#### Orchestrate with Cantaloupe

It may be worth investigating how performant Cantaloupe can be with an `S3Source` and plenty of diskspace for a [`DerivativeCache`](https://cantaloupe-project.github.io/manual/5.0/caching.html#Derivative%20Cache). This would remove the need to copy images via the `orchestrator` service - allowing that to handle reverse proxying requests only.

The advantages to Cantaloupe handling the "orchestration" is that:

* It would remove complexity from the dotnet code and let it purely focus on reverse proxy behaviour.
* Would reduce the number of moving part. No need for scavenger service as we can use Cantaloupe cache workers.
* Removes the need to have the Orchestrator and Cantaloupe servers co-located on same hardware or sharing a NAS.

The disadvantages are:

* Could remove some potential optimisations, like `"OrchestrateOnInfoJson"` optimisation (although this could be accomplished by alternative means).
* Cache invalidation - if a source image is updated then Cantaloupe cache would need to be cleared. This is not fully solved via Orchestrator approach, see [#233](https://github.com/dlcs/protagonist/issues/233)

### Info.json

IIPImage uses a set 256x256 tile size. This fact makes it quick and easy for the DLCS to serve info.json requests from templates.

Cantaloupe has logic to return the tiles sizes that will be most efficiently delivered. Tile delivery should be faster but it means that the dotnet code will need to proxy info.json requests to Cantaloupe. 

Info.json responses are highly cacheable, however the basic info.json generated by Cantaloupe will need to be augmented with some DLCS specific services and identifiers.

We will need to experiment to see how best to augment the info.json but it seems to make sense to happen at the dotnet level as that has access to all required information.

One option is to store in the DLCS database whether the image source is natively tiled - if it _is_ then Cantaloupe will generated sizes based on the width + height. However, if it _isn't_ then it uses a configuration value and could easily be mimiced by the dotnet code. Given that we generated JPEG2000 derivatives most images _will_ be natively tiled so this may be of limited use.

## Future Developments

Cantaloupe has the ability to render individual PDF pages or single frames of a video file. This could lead to some powerful future developments such as [thumbnail services for AV + PDFs](https://github.com/dlcs/protagonist/issues/73).

## Other Considerations

If not orchestrating we may need to change how we're storing images to help with Format Inference, dependant on source.

Cantaloupe has a [delegate system](https://cantaloupe-project.github.io/manual/5.0/delegate-system.html) that allows it to be extended with custom Java code. This is a powerful feature but should be used sparingly to avoid tying the DLCS to this image-server implementation.

## Further Reading

* [Storage and Orchestration RFC](https://github.com/dlcs/protagonist/blob/master/docs/rfcs/002-storage-and-orchestration.md#orchestration).
* [`image-server`](https://github.com/dlcs/protagonist/labels/image-server) tagged issues.
