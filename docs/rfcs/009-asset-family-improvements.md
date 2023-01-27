# Asset Family Improvements

There are currently 3 `AssetFamily` values available in the DLCS, **I**mage (`I`), **T**imebased (`T`) and **F**ile (`F`).

The families dictate both how an asset is delivered and how it is processed.

Driven by the requirements for Wellcome's Born Digital work, we have a need to have more control over how assets are delivered and processed - `AssetFamily` is not granular enough.

In summary, `AssetFamily` is now:

* A general indication of what the source file is, this will dictate what values are required (affecting validation rules) and what sort of transcoding/conversion can happen.
* A preset 'delivery-channel'.
* A preset image-optimisation policy (`iop`).

## Asset Delivery Channel

To accomodate the need to specify how an asset is delivered, we will introduce a new `delivery-channel` concept which specifies _how_ an asset can be requested and _if_ the engine needs to process. 

Valid values are:
* `file` - asset will be available on the 'file' path, `/file/{customer}/{space}/{asset}`. This means that the original file is available for download (which could be source mp3, mp4, jpeg etc), possibly in addition to derivatives.
* `iiif-av` - a timebased derivative of the asset can be streamed on path `/iiif-av/{customer}/{space}/{asset}/{timebased-request}` (e.g. `iiif-av/1/2/foo/full/max/default.mp3`).
* `iiif-img` - a IIIF image service is available on path `/iiif-img/{customer}/{space}/{asset}/{image-request}` (e.g. `iiif-img/1/2/bar/info.json` or `iiif-img/1/2/bar/0,0,1024,2048/!512,512/0/default.jpg`).
* `thumbs` - a Level 0 IIIF image service is available on path `/iiif-img/{customer}/{space}/{asset}/{image-request}` (e.g. `iiif-img/1/2/bar/info.json` or `iiif-img/1/2/bar/0,0,1024,2048/!512,512/0/default.jpg`).

This value will be stored in a new column on the `Images` table.

## Image Optimisation Policy (Engine)

While `delivery-channel` is primarily used for how the asset is delivered in Orchestrator, the Engine will use it to decide _if_ any processing is required. 

_How_ that processing happens is determined by `AssetFamily`:
* `T` - AWS ElasticTranscoder
* `I` - Appetiser
* `F` - nothing - use source file

_What_ that processing is will be determined by `iop`, each `iop` contains definition used by processor.

e.g. If an `I` asset is created with only `"file"` delivery-channel then there would be no transcoding carried out. However, as soon as the "iiif-img" or "thumbs" channel is added the iop would be utilised. This delays any unnecessary/potentially costly transcoding operations until required.

### No Transcode Policy

PR [#424](https://github.com/dlcs/protagonist/pull/424) introduced an explicit "no-transcode" policy (key of `"none"`) which signified that the source image does not need to be transcoded - it is web-friendly already.

This is an explicit policy for when we won't transcode a file and the default for `"file"` delivery-channel assets.

## Validation Requirements

The `AssetFamily` will affect the general validation rules when creating an asset. 

These will be:

| Family | MediaType | Delivery Channel   | Rules                                           |
| ------ | --------- | ------------------ | ----------------------------------------------- |
| `T`    | `audio/*` | `file` only        | duration is required                            |
| `T`    | `audio/*` | includes `iiif-av` | duration must not be provided                   |
| `T`    | `video/*` | `file` only        | duration, width and height are required         |
| `T`    | `video/*` | includes `iiif-av` | duration, width and height must not be provided |
| `I`    | `image/*` | `file` only        | width and height are required                   |
| `I`    | `image/*` | includes `iiif-av` | width and height must not be provided           |
| `F`    | `*`       | `file`             | no dimensions are valid                         |

## Asset Family Presets

`AssetFamily` values of `I`, `T` and `F` serve as both a general grouping for items and a set of presets for delivery-channel and imageOptimisationPolicy, which can be overridden.

To replicate the behaviour of the current DLCS the defaults for each family would be:

* `I` is (delivery-channel: "iiif-img,thumbs") and (iop: "fast-higher")
* `T` is (delivery-channel: "iiif-av") and (iop: "video-max" or "audio-max" depending on media-type)
* `F` is (delivery-channel: "file") and (iop: "none")

## Required Changes

The possible outcomes are now:

| Asset Family | MediaType | Delivery Channel | IOP              | Thumbs Policy | Engine Behaviour                                            | Asset Delivery (Orchestrator/Thumbs)                        |
| ------------ | --------- | ---------------- | ---------------- | ------------- | ----------------------------------------------------------- | ----------------------------------------------------------- |
| I            | image/*   | file             |                  |               | Copy from Origin to storage bucket                          | Stream from storage bucket                                  |
| I            | image/*   | iiif-img         | {image-specific} |               | Download + convert to JP2. Upload JP2 to storage bucket     | Proxy to thumbs/special-server/cantaloupe                   |
| I            | image/*   | iiif-av          |                  |               | **invalid**                                                 |                                                             |
| I            | image/*   | thumbs           | {image-specific} | {required}    | Generate thumbnails, upload to thumbs bucket.               | Proxy thumbnail from S3                                     |
| T            | audio/*   | file             |                  |               | Copy from Origin to storage bucket                          | Stream from storage bucket                                  |
| T            | audio/*   | iiif-img         |                  |               | **invalid**                                                 |                                                             |
| T            | audio/*   | iiif-av          | {ET-specific}    |               | Elastic-Transcoder. Move to storage bucket.                 | 302 or pass through proxying depending on Auth requirements |
| T            | audio/*   | thumbs           |                  |               | **invalid**                                                 |                                                             |
| T            | video/*   | file             |                  |               | Copy from Origin to storage bucket                          | Stream from storage bucket                                  |
| T            | video/*   | iiif-img         | _??_             |               | _Not currently supported but could generate key frames_     | _Could work similar to image handling, proxy to Cantaloupe_ |
| T            | video/*   | iiif-av          | {ET-specific}    |               | Elastic-Transcoder. Move to storage bucket.                 | 302 or pass through proxying depending on Auth requirements |
| T            | video/*   | thumbs           | _??_             | _required_    | _Not currently supported but could generate from key frame_ | _Could work similar to image - predefined thumbs_           |
| F            | \*/*      | file             |                  |               | Copy from Origin to storage bucket                          | Stream from storage bucket                                  |
| F            | \*/*      | iiif-img         | _??_             |               | _Not currently supported but could generate e.g. PDF pages_ | _Could work similar to image handling, proxy to Cantaloupe_ |
| F            | \*/*      | iiif-av          |                  |               | **invalid**                                                 |                                                             |
| F            | \*/*      | thumbs           | _??_             | _required_    | _Not currently supported but could generate from key frame_ | _Could work similar to image - predefined thumbs_           |

Notes on the above table:
* _customer-origin-strategy_ logic will be used for fetching from Origin for `file` delivery channels. In the future we could expand the 'optimised-origin' concept for Engine not to copy and Orchestrator to stream from origin.
* Absence of an `iop` in API payload indicates use default policy.
* _italicized_ items are possible improvements in the future and hints/suggestions at how it could work.

### A Note on File Handling

Currently `F` assets are streamed from their Origin when requested. When a `F` asset is created processing stops at the API and the Engine is not notified of `F` assets, they are immediately marked as complete.

This behaviour was written under the assumption that a) the Origin is stable and b) the uploaded binary is relatively small (e.g. a PDF or spreadsheet). However, this may no longer be the case as we could be serving untranscoded Audio or Video files.

The API will be updated to notify the Engine of assets that have `file` delivery-channel and the Engine will copy from Origin to s3 storage.

## Related Tickets

* https://github.com/dlcs/protagonist/issues/393
* https://github.com/dlcs/protagonist/issues/384
* https://github.com/dlcs/protagonist/issues/62

## Examples

Below are some sample payloads and resulting behaviour to illustrate above:

### Basic image

The request:

```
PUT /customers/1/spaces/2/images/foo
{
    "origin": "https://example.com/large-image.tiff,
    "family": "I",
    "deliveryChannel": "iiif-img,thumbs",
    "imageOptimisationPolicy": "faster-higher",
    "thumbnailPolicy": "default",
    "mediaType": "image/jpeg"
}
// OR
{
    "origin": "https://example.com/large-image.tiff,
    "family": "I",
    "mediaType": "image/jpeg"
}
```

Would result in Engine generating JP2 + thumbs and the asset being available on the following paths:

* `/iiif-img/1/2/foo/*`
* `/thumbs/1/2/foo/*`

### Image only

The request:

```
PUT /customers/1/spaces/2/images/foo
{
    "origin": "https://example.com/large-image.tiff,
    "family": "I",
    "deliveryChannel": "iiif-img",
    "imageOptimisationPolicy": "faster-higher",
    "mediaType": "image/jpeg"
}
// OR
{
    "origin": "https://example.com/large-image.tiff,
    "deliveryChannel": "iiif-img",
    "family": "I",
    "mediaType": "image/jpeg"
}
```

Would result in Engine generating JP2 only and in the asset being available on the:

* `/iiif-img/1/2/foo/*` - we would lose optimisation of Orchestrator using `/thumbs/`

### Image and raw

The request:

```
PUT /customers/1/spaces/2/images/foo
{
    "origin": "https://example.com/large-image.tiff,
    "family": "I",
    "deliveryChannel": "file,iiif-img,thumbs",
    "imageOptimisationPolicy": "faster-higher",
    "thumbnailPolicy": "default",
    "mediaType": "image/jpeg"
}
```

Would result in Engine generating JP2 + thumbs and copying asset to DLCS origin bucket. The asset is available on:

* `/iiif-img/1/2/foo/*`
* `/thumbs/1/2/foo/*`
* `/file/1/2/foo` - would return large-image.tiff, streamed from DLCS origin bucket (_not_ "origin" location)

### Video, raw source file only

The request:

```
PUT /customers/1/spaces/2/images/foo
{
    "origin": "https://example.com/web-optimised.mp4,
    "family": "T",
    "deliveryChannel": "file",
    "imageOptimisationPolicy": "none",
    "mediaType": "video/mp4",
    "duration": 12000,
    "width": 100,
    "max": 400
}
```

Would result in Engine copying asset to DLCS origin bucket only. Dimensions required as this is not transcoded.

Asset is available on:

* `/file/1/2/foo` - would return web-optimised.mp4, streamed from DLCS origin bucket (_not_ "origin" location)

### Video and source

The request:

```
PUT /customers/1/spaces/2/images/foo
{
    "origin": "https://example.com/web-optimised.mp4,
    "family": "T",
    "deliveryChannel": "file,iiif-av",
    "imageOptimisationPolicy": "none",
    "mediaType": "video/mp4"
}
```

Would result in Engine calling ElasticTranscoder and copying asset to DLCS origin bucket only. Asset is then available on:

* `/file/1/2/foo` - would return web-optimised.mp4, streamed from DLCS origin bucket (_not_ "origin" location)
* `/iiif-av/1/2/foo/*` - handles generated derivatives.

## Questions

* Is "no-transcode" policy required? Implemented in [#424](https://github.com/dlcs/protagonist/pull/424) but can be used synonomously with "file" delivery-channel.
* Do we want to require `d,w,h` dimensions for `"file"` delivery-channel, as details above? `"file"` delivery-channel indicates you want to serve the bytes as you know the origin is in an appropriate format. Does that then mean you must have dimensions? e.g. I don't need to know how many pages are in a PDF for it to be served, should I need to know how long a video is for it to be served?
* We need to maintain `AssetFamily` for NQ and single-item manifest generation to know what service type(s) to add?
* Would media-type be a better indicator of what `AssetFamily` is used for, above? Is this too loose?