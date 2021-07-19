# Image Processor

## Context

The Image-Processor, is an application used to generate tile friendly images from standard sources and associated derivatives. It is part of the [Engine](006-Engine-Image.md) application.

There are currently 2 Image-Processor implementations available: [tizer](https://github.com/tomcrane/jp2iser) and [appetiser](https://github.com/digirati-co-uk/appetiser).

> Note: this image-processor is not to be confused with an IIIF Image-Server (like [Loris](https://github.com/loris-imageserver/loris), [Cantaloupe](https://cantaloupe-project.github.io/) or [IIPImage](https://github.com/ruven/iipsrv)).

## Overview

The image-server has a single endpoint for ingesting images. This endpoint takes a payload that contains details of the source image  along with some parameters that define _what_ to generate, _how_ to do it and _where_ to put the resulting files.

There are 2 "modes" that control _what_ is generated:

* ingest: this will generate a tile-optimised image and associated thumbnails from the specified source image.
* derivatives-only: this will _only_ generate thumbnails (the assumption being that the original image is tile-optimised already).

Regardless of which "mode" is used, a list of thumbnail sizes will be provided, which tell of the size of the longest edge.

An "optimisation" parameter tells the Image-Processor _how_ to do it, for example:

* JPEG2000 vs Pyramidal Tiff.
* fast and lossy vs slower and max quality.
* what colour profiles to use.

The Image-Processor may also be able to identify if the source image is already tile-optimised, and if such it can skip that expensive step and only generate thumbnails instead.

There is an assumption that the calling application and the Image-Processor share a local drive where source and generated images are placed. This is an optimisation to remove the need to stream images back and forth over HTTP.

Generation is CPU intensive so there is a 1:1 'pairing' of Engines and Image-Processors. Multiple Engines calling a single Image-Processor could work if the Image-Processor had enough resources but keeping the ratio at 1:1 allows good performance without sizable hardware requirements.

> Note: The Image-Processor has no notion of authentication or what sizes are available publicly, it simply generates the files.