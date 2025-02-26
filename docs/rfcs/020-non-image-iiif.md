# Content Resources for Non-Image Assets

There are two places that output IIIF content resources for Assets:

* `/iiif-resource/` -> NamedQuery (NQ) manifest projection
* `/iiif-manifest/` -> "single item" manifest

Delivery-channels determine how an asset is available. Only 'image' assets, available on `"iiif-img"` and/or `"thumbs"` are correctly output as content resources, any other delivery-channels are effectively ignored, or the behaviour is undefined.

This RFC outlines how we will output content resources for other delivery-channels. At a high level this will be:

* `iiif-av` for Audio or Video. Output will be either:
  * If there is a single transcoded output: A single `body` of type `"Sound"` or `"Video"` with `"id"` referencing the transcoded web-friendly derivative.
  * If there are multiple transcoded outputes: A choice, containing multiple `"Sound"` or `"Video"` as mentioned above
* `file` delivery channel will be output as either:
  * When Asset is also available on `iiif-av` or `iiif-img` channel then we can include a `"rendering"` on the existing canvas as the other channel will include the content painted onto the canvas.
  * When Asset is only available on `file` then we will use the approach from https://github.com/wellcomecollection/docs/tree/main/rfcs/046-born-digital-iiif. The origin file will be available as a `"rendering"` as above but there will be a placeholder image painted onto the canvas. As detailed in linked RFC this introduces some custom `behavior` values, requiring a custom context, but maintains valid IIIF.

## A note on versions

Both NQ and single-item manifests can be requested as IIIF Presentation 2.1 or 3. 

The examples shown here are for _v3 only_. v2.1 manifests will still be returned but will only contain image resources as it doesn't support non-image assets.

## `iiif-img` and `thumbs` channel

As mentioned above, Image output is already supported. This will be rendered as ImageService(s) on the canvas if `iiif-img` is available, including a `"thumbnail"` if `thumbs` channel is available.

Image rendering will need extended to handle `file` delivery channel.

### Example

Below is an example of a NQ manifest that contains 1 image, which has `iiif-img`, `thumbs` and `file` channel available.

Note that the `"rendering"` uses the mediaType of the _origin_ file. The imageService use the mediaType of the derivative. `"thumbnail"` service omitted for brevity.

```json
{
    "@context": "http://iiif.io/api/presentation/3/context.json",
    "type": "Manifest",
    "id": "https://dlcs.example/020-non-image-iiif.md",
    "items": [
        {
            "id": "https://dlcs.example/iiif-img/2/1/bar/canvas/c/1",
            "type": "Canvas",
            "width": 2550,
            "height": 3300,
            "thumbnail": [
                {
                    "id": "https://dlcs.example/thumbs/2/1/bar/155,200/0/default.jpg",
                    "type": "Image",
                    "format": "image/jpeg",
                    "service": []
                }
            ],
            "items": [
                {
                    "id": "https://dlcs.example/iiif-img/2/1/bar/canvas/c/1/page",
                    "type": "AnnotationPage",
                    "items": [
                        {
                            "id": "https://dlcs.example/iiif-img/2/1/bar/canvas/c/1/image",
                            "type": "Annotation",
                            "motivation": "painting",
                            "body": {
                                "id": "https://dlcs.example/iiif-img/2/1/bar/full/791,1024/0/default.jpg",
                                "type": "Image",
                                "width": 791,
                                "height": 1024,
                                "format": "image/jpeg",
                                "service": [
                                    {
                                        "@context": "http://iiif.io/api/image/3/context.json",
                                        "id": "https://dlcs.example/iiif-img/2/1/bar",
                                        "type": "ImageService3",
                                        "profile": "level2",
                                        "width": 2550,
                                        "height": 3300
                                    }
                                ]
                            },
                            "target": "https://dlcs.example/iiif-img/2/1/bar/canvas/c/1"
                        }
                    ]
                }
            ],
            "rendering": [
                {
                    "width": 2550,
                    "height": 3300,
                    "id": "https://dlcs.example/file/2/1/bar",
                    "type": "Image",
                    "format": "image/tiff"
                }
            ]
        }
    ]
}
```

## `iiif-av` channel

Audio and Video will be rendered similarly, with only minimal differences:
* `"type"` - Video are rendered as `"type": "Video"` with audio rendered as `"type": "Sound"`
* Video has spatial (width, height) and temporal measurements (duration) but Audio only has temporal.

### Example

Below is an example of a NQ manifest that contains 2 assets:

* `2/1/foo` - a video only available on `iiif-av` channel, as a mp4 and webm transcode. This is rendered as a Choice in the first canvas
* `2/1/bar` - audio file available on `iiif-av`, as a single mp3 transcode, and `file` channel. The transcode is on `Sound` on canvas, with the origin file available as a `"rendering"`

Note that the `"rendering"` uses the mediaType of the _origin_ file. The transcodes use the mediaType of the _transcoded_ file.

```json
{
    "@context": "http://iiif.io/api/presentation/3/context.json",
    "type": "Manifest",
    "id": "https://dlcs.example/020-non-image-iiif.md",
    "items": [
        {
            "id": "https://dlcs.example/iiif-av/2/1/foo/canvas/c/1",
            "type": "Canvas",
            "width": 200,
            "height": 300,
            "duration": 123.42,
            "items": [
                {
                    "id": "https://dlcs.example/iiif-av/2/1/foo/canvas/c/1/page",
                    "type": "AnnotationPage",
                    "items": [
                        {
                            "id": "https://dlcs.example/iiif-av/2/1/foo/canvas/c/1/image",
                            "type": "Annotation",
                            "motivation": "painting",
                            "body": {
                                "type": "Choice",
                                "items": [
                                    {
                                        "width": 200,
                                        "height": 300,
                                        "duration": 123.42,
                                        "id": "https://dlcs.example/iiif-av/2/1/foo/full/full/max/max/0/default.mp4",
                                        "type": "Video",
                                        "format": "video/mp4"
                                    },
                                    {
                                        "width": 200,
                                        "height": 300,
                                        "duration": 123.42,
                                        "id": "https://dlcs.example/iiif-av/2/1/foo/full/full/max/max/0/default.webm",
                                        "type": "Video",
                                        "format": "video/webm"
                                    }
                                ]
                            },
                            "target": "https://dlcs.example/iiif-av/2/1/foo/canvas/c/1"
                        }
                    ]
                }
            ]
        },
        {
            "id": "https://dlcs.example/iiif-av/2/1/bar/canvas/c/2",
            "type": "Canvas",
            "duration": 999.99,
            "items": [
                {
                    "id": "https://dlcs.example/iiif-av/2/1/bar/canvas/c/2/page",
                    "type": "AnnotationPage",
                    "items": [
                        {
                            "id": "https://dlcs.example/iiif-av/2/1/bar/canvas/c/2/image",
                            "type": "Annotation",
                            "motivation": "painting",
                            "body": {
                                "duration": 999.99,
                                "id": "https://dlcs.example/iiif-av/2/1/bar/full/max/0/default.mp3",
                                "type": "Sound",
                                "format": "audio/mp3"
                            },
                            "target": "https://dlcs.example/iiif-av/2/1/bar/canvas/c/2"
                        }
                    ]
                }
            ],
            "rendering": [
                {
                    "duration": 999.99,
                    "id": "https://dlcs.example/file/2/1/bar",
                    "type": "Sound",
                    "format": "audio/flac"
                }
            ]
        }
    ]
}
```

### Available Transcodes

We currently don't have any record of what transcodes each Asset has available. To render the above we need to efficiently identify what these are.

We encountered this same issue when implementing `thumbs` delivery-channel. The solution was to implement the [AssetApplicationMetadata](016-asset-metadata.md) table. We will need to extend this to store transcoded types and their location (and, if known from transcoding operation, their mediaType). This can then be used to populate the Audio/Sound body.

AV files that were generated prior to this date aren't guaranteed to have an `AssetApplicationMetadata` record so we will need a fallback. From the `"Images"` record we can only see the 'friendly' derivative name (e.g. `audio-mp3-128`), not what this is mapped to or what it actually means, only Engine knows that. One option would be able to have an Engine endpoint that can take `audio-mp3-128` and return details of what types this would transcode to.

## File

> [!IMPORTANT]
> That this section is for those items that have `file` as the only delivery-channel.

See Wellcome RFC https://github.com/wellcomecollection/docs/tree/main/rfcs/046-born-digital-iiif for where this proposal originated from and https://iiif.wellcomecollection.org/presentation/SAPOP/B/2/9 for a concrete example.

The original file will be available as a `"rendering"` (like other `file` channels) but there is no `iiif-img` or `iiif-av` channel available for painting content onto the canvas. Instead we will use a placeholder image. Unlike the linked Wellcome example, we will use a generic placeholder image for all content, rather than one that varies by type.


Notes on below:
* `"type"` of rendering will need to be mapped from mediaType of asset, falling back to `DataSet`. Using the "type" part of the mediaType should be enough initially.
* 1000x1000 size is from the placeholder image
* Custom context required due to `"placeholder"` and `"original"` behaviors
* Custom context introduced for Wellcome will eb used as this is dereferenceable (or will be).

```json
{
    "@context": [
        "https://iiif.wellcomecollection.org/extensions/born-digital/context.json",
        "http://iiif.io/api/presentation/3/context.json",
    ],
    "type": "Manifest",
    "id": "https://dlcs.example/020-non-image-iiif.md",
    "items": [
        {
            "id": "https://dlcs.example/file/2/1/bar/canvas/c/1",
            "type": "Canvas",
            "width": 1000,
            "height": 1000,
            "behavior": ["placeholder"],
            "items": [
                {
                    "id": "https://dlcs.example/iiif-img/2/1/bar/canvas/c/1/page",
                    "type": "AnnotationPage",
                    "items": [
                        {
                            "id": "https://dlcs.example/iiif-img/2/1/bar/canvas/c/1/image",
                            "type": "Annotation",
                            "motivation": "painting",
                            "body": {
                                "id": "https://dlcs.example/static/placeholder.jpg",
                                "type": "Image",
                                "width": 1000,
                                "height": 1000,
                                "format": "image/jpeg"
                            },
                            "target": "https://dlcs.example/iiif-img/2/1/bar/canvas/c/1"
                        }
                    ]
                }
            ],
            "rendering": [
                {
                    "duration": 999.99,
                    "id": "https://dlcs.example/file/2/1/bar",
                    "type": "Sound",
                    "format": "audio/flac",
                    "behavior": ["original"]
                }
            ]
        }
    ]
}
```

## Implementation Note

The `IIIFCanvasFactory` class is used to construct canvases for both single-item and NQ manifests. In theory making changes in there should mean both places gain that logic.

`IIIFCanvasFactory` returns a list of canvases only, we will need some way to signal that a new context needs to be added to the manifest.

## Questions

* What should the placeholder contain?
* What path should the placeholder be available on? `/static/placeholder.jpg` on orchestrator is used above - is that enough?
* Should we implement the `AssetApplicationMetadata` fallback of calling engine? Or if we can't find AAM do we not render (so as-is)?