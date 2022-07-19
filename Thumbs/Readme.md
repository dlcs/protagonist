# Thumbs

Middleware that provides simplified handling of requests for thumbnails.

Parses incoming IIIF image requests to determine which thumbnail to serve by finding the longest edge.

Supports requests for the following IIIF Size Parameters:

* `w,`
* `,h`
* `w,h`
* `!w,h`
* `max`
* `full`

## Storage

This app expects the following layout in S3 (can be configured to generate this layout, see [configuration](#configuration)). The `/open` and `/auth` paths are determined by the images `ThumbnailPolicy`. Only thumbnails in the `/open` bucket will be returned.

"Low.jpg" is always created as the largest sized thumbnail as this is used for generating resources like PDFs.

```
/thumbs-bucket/2/1/image-id
    /open
        100.jpg
        200.jpg
    /auth
        400.jpg
        1024.jpg
    s.json
    low.jpg
```

Where sizes.json looks like this (for example):

```json
{
    "o": [
        [200,127],
        [100,64]
    ],
    "a": [
        [1024,651],
        [400,254]
    ]
}
```

## Configuration

There are a few app settings that can control the behaviour of the application:

### `Thumbs:EnsureNewThumbnailLayout`

* `True` - when a request is received the `ThumbReorganiser` class will ensure that the above format exists in S3 by consulting an images `ThumbnailPolicy` and copying thumbnails from existing level 0 image API paths in same bucket.
* `False` - when a request is received the assumption is that the above format exists in S3. If it doesn't a 404 will be returned.

### `Thumbs:Resize`

* `True` - if an exact matching thumbnail is not found, we will attempt to resize the next largest thumbnail to match requirements.
* `False` - if an exact matching thumbnail is not found, the request will return 404.

### `Thumbs:Upscale`

* `True` - when resizing, the next largest thumbnail will be used to resize. If a larger thumbnail is not found, and `Upscale = true` then we will upscale the next smallest.
* `False` - upsizing is not attempted.

### `Thumbs:UpscaleThreshold`

Integer value - the maximum % that we will attempt to increase a thumbnail by. If required upscaling exceeds this a 404 will be returned.

For no limit use `0`.

### `RespondsTo`

By default intercepts all requests to `/thumbs/` (e.g. `https://my.dlcs/thumbs/*`) but the path can be configured with the `RespondsTo` app setting.

## Deployment

See Dockerfile.Thumbs in the solution root for deployment artifacts.

```bash
cd..
docker build -f Dockerfile.Thumbs -t Thumbs:local .
```

## Technology :robot:

There are a variety of technologies used across the projects, including:

* [ImageSharp](https://github.com/SixLabors/ImageSharp) - high performance graphics library. Used for resizing thumbnails.