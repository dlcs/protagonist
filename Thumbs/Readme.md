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

```
/thumbs-bucket/2/1/image-id
    /open
        100.jpg
        200.jpg
    /auth
        400.jpg
        1024.jpg
    s.json
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

Can run in 2 "modes" via the `Repository:EnsureNewThumbnailLayout` app setting:

* `True` - when a request is received the `ThumbReorganiser` class will ensure that the above format exists in S3 by consulting an images `ThumbnailPolicy` and copying thumbnails from existing level 0 image API paths in same bucket.
* `False` - when a request is received the assumption is that the above format exists in S3. If it doesn't a 404 will be returned.

By default intercepts all requests to `/thumbs/` (e.g. `https://my.dlcs/thumbs/*`) but the path can be configured with the `RespondsTo` app setting.

## Deployment

See Dockerfile.Thumbs and Jenkinsfile.Thumbs in the solution root for deployment artifacts.