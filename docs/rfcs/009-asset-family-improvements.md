# Asset Family Improvements

There are currently 3 `AssetFamily` values available in the DLCS, **I**mage (`I`), **T**imebased (`T`) and **F**ile (`F`).

The families dictate both how an asset is delivered and how it is processed.

Driven by the requirements for Wellcome's Born Digital work, we have a need to have more control over how assets are delivered and processed - `AssetFamily` is not granular enough.

In summary, `AssetFamily` will be removed as a concept, replaced with:

* The "type" part of provided `mediaType` as an indiciation of what the source file is. This will dictate what dimension values are required (affecting validation rules) and what sort of transcoding/conversion can happen.
* A preset 'delivery-channel'.
* A preset image-optimisation policy (`iop`).

## Asset Delivery Channel

To accomodate the need to specify how an asset is delivered, we will introduce a new `delivery-channel` concept which specifies _how_ an asset can be requested and _if_ the engine needs to process. 

Valid values are:
* `file` - asset will be available on the 'file' path, `/file/{customer}/{space}/{asset}`. This means that the original file is available for download (which could be source mp3, mp4, jpeg etc), possibly in addition to derivatives.
* `iiif-img` - a IIIF image service is available on path `/iiif-img/{customer}/{space}/{asset}/{image-request}` (e.g. `iiif-img/1/2/bar/info.json` or `iiif-img/1/2/bar/0,0,1024,2048/!512,512/0/default.jpg`).
* `iiif-av` - a timebased derivative of the asset can be streamed on path `/iiif-av/{customer}/{space}/{asset}/{timebased-request}` (e.g. `iiif-av/1/2/foo/full/max/default.mp3`).

This value will be stored in a new column on the `Images` table.

## Image Optimisation Policy (Engine)

While `delivery-channel` is primarily used for how the asset is delivered in Orchestrator, the Engine will use it to decide _if_ any processing is required. 

_How_ that processing happens is determined by the type of `mediaType`, with specific details provided by `iop`:
* `audio` or `video`: AWS ElasticTranscoder (formerly `T` family)
* `image`: Appetiser (formerly `I` family)
* Any other type (e.g. `application`, `multipart` etc): do nothing - use source file (formerly `F` family)

_What_ that processing is will be determined by `iop`, each `iop` contains definition used by processor.

e.g. If an `image/jpeg` asset is created with only `"file"` delivery-channel then there would be no transcoding carried out. However, as soon as the "iiif-img" or "thumbs" channel is added the iop would be utilised. This delays any unnecessary/potentially costly transcoding operations until required.

### No Transcode Policy

PR [#424](https://github.com/dlcs/protagonist/pull/424) introduced an explicit "no-transcode" policy (key of `"none"`) which signified that the source image does not need to be transcoded - it is web-friendly already.

This is a placeholder policy for the `"file"` delivery-channel - it is only valid for `"file"` delivery-channel.

## Validation Requirements

The `mediaType` will affect the general validation rules when creating an asset. 

These will be:

| MediaType | Delivery Channel     | Rules                                           |
| --------- | -------------------- | ----------------------------------------------- |
| `audio/*` | `file` only          | duration is optional                            |
| `audio/*` | includes `timebased` | duration must not be provided                   |
| `video/*` | `file` only          | duration, width and height are optional         |
| `video/*` | includes `timebased` | duration, width and height must not be provided |
| `image/*` | `file` only          | width and height are optional                   |
| `image/*` | includes `image`     | width and height must not be provided           |
| `*`       | `file`               | no dimensions are valid                         |

> Note on above:
> * `*` mediaType represents any that is _not_ audio/\*, video/\* or image/\*
> Optional dimensions are defaulted to 0 if not provided. This may cause issues rendering via NQ's but allows the file to be served.

## Presets

The `mediaType` type value will serve as a set of presets for delivery-channel and imageOptimisationPolicy, either of which can be overridden.

To replicate the behaviour of the current DLCS the defaults for different `mediaTypes` would be would be:

* `image/*` is (delivery-channel: "iiif-img,thumbs") and (iop: "fast-higher")
* `audio/*` is (delivery-channel: "timebased") and (iop: "audio-max")
* `video/*` is (delivery-channel: "timebased") and (iop: "video-max")
* `<fallthrough>` is (delivery-channel: "file") and (iop: "none")

## Required Changes

The possible outcomes are now:

| Old AssetFamily | MediaType | Delivery Channel | IOP              | Thumbs Policy | Engine Behaviour                                            | Asset Delivery (Orchestrator/Thumbs)                        |
| --------------- | --------- | ---------------- | ---------------- | ------------- | ----------------------------------------------------------- | ----------------------------------------------------------- |
| I               | `image/*` | file             |                  |               | Copy from Origin to storage bucket                          | Stream from storage bucket                                  |
| I               | `image/*` | iiif-img         | {image-specific} |               | Download + convert to JP2. Upload JP2 to storage bucket     | Proxy to thumbs/special-server/cantaloupe                   |
| I               | `image/*` | timebased        |                  |               | **invalid**                                                 |                                                             |
| I               | `image/*` | thumbs           | {image-specific} | {required}    | Generate thumbnails, upload to thumbs bucket.               | Proxy thumbnail from S3                                     |
| T               | `audio/*` | file             |                  |               | Copy from Origin to storage bucket                          | Stream from storage bucket                                  |
| T               | `audio/*` | iiif-img         |                  |               | **invalid**                                                 |                                                             |
| T               | `audio/*` | timebased        | {ET-specific}    |               | Elastic-Transcoder. Move to storage bucket.                 | 302 or pass through proxying depending on Auth requirements |
| T               | `audio/*` | thumbs           |                  |               | **invalid**                                                 |                                                             |
| T               | `video/*` | file             |                  |               | Copy from Origin to storage bucket                          | Stream from storage bucket                                  |
| T               | `video/*` | iiif-img         | _??_             |               | _Not currently supported but could generate key frames_     | _Could work similar to image handling, proxy to Cantaloupe_ |
| T               | `video/*` | timebased        | {ET-specific}    |               | Elastic-Transcoder. Move to storage bucket.                 | 302 or pass through proxying depending on Auth requirements |
| T               | `video/*` | thumbs           | _??_             | _required_    | _Not currently supported but could generate from key frame_ | _Could work similar to image - predefined thumbs_           |
| F               | `*`       | file             |                  |               | Copy from Origin to storage bucket                          | Stream from storage bucket                                  |
| F               | `*`       | iiif-img         | _??_             |               | _Not currently supported but could generate e.g. PDF pages_ | _Could work similar to image handling, proxy to Cantaloupe_ |
| F               | `*`       | timebased        |                  |               | **invalid**                                                 |                                                             |
| F               | `*`       | thumbs           | _??_             | _required_    | _Not currently supported but could generate from key frame_ | _Could work similar to image - predefined thumbs_           |

> Notes on the above table:
> * _customer-origin-strategy_ logic will be used for fetching and streaming from Origin for `file` delivery channels.
> * Absence of an `iop` in API payload indicates use default policy.
> * `*` mediaType represents any that is _not_ audio/\*, video/\* or image/\*
> * _italicized_ items are possible improvements in the future and hints/suggestions at how it could work.

### A Note on File Handling

Currently `F` assets are streamed from their Origin when requested. When a `F` asset is created processing stops at the API and the Engine is not notified of `F` assets, they are immediately marked as complete.

This behaviour was written under the assumption that a) the Origin is stable and b) the uploaded binary is relatively small (e.g. a PDF or spreadsheet). However, this may no longer be the case as we could be serving large untranscoded Audio or Video files.

The API will be updated to notify the Engine of assets that have `file` delivery-channel and the Engine will copy from Origin to S3 storage (or not - depending on customer-origin-strategy).

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
    "deliveryChannel": "iiif-img,thumbs",
    "imageOptimisationPolicy": "faster-higher",
    "thumbnailPolicy": "default",
    "mediaType": "image/jpeg"
}
// OR
{
    "origin": "https://example.com/large-image.tiff,
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
    "deliveryChannel": "iiif-img",
    "imageOptimisationPolicy": "faster-higher",
    "mediaType": "image/jpeg"
}
// OR
{
    "origin": "https://example.com/large-image.tiff,
    "deliveryChannel": "iiif-img",
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
    "deliveryChannel": "file",
    "imageOptimisationPolicy": "none",
    "mediaType": "video/mp4",
    "duration": 12000,
    "width": 100,
    "max": 400
}
```

Would result in Engine copying asset to DLCS origin bucket only. Dimensions provided as this is not transcoded.

Asset is available on:

* `/file/1/2/foo` - would return web-optimised.mp4, streamed from DLCS origin bucket (_not_ "origin" location)

### Video and source

The request:

```
PUT /customers/1/spaces/2/images/foo
{
    "origin": "https://example.com/web-optimised.mp4,
    "deliveryChannel": "file,timebased",
    "imageOptimisationPolicy": "none",
    "mediaType": "video/mp4"
}
```

Would result in Engine calling ElasticTranscoder and copying asset to DLCS origin bucket only. Asset is then available on:

* `/file/1/2/foo` - would return web-optimised.mp4, streamed from DLCS origin bucket (_not_ "origin" location)
* `/iiif-av/1/2/foo/*` - handles generated derivatives.

## Notes

* "mediaType" is now elevated to have a lot more affect on how assets are processed. This means that a video file with "application/mp4" would need to be registered as "video/mp4".
* "AssetFamily" column will be removed from Images table. The requirement on "mediaType" being provided via API will allow us to deprecate this.