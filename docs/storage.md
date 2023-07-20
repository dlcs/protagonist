## Storage counters

There are 2 tables that track storage details:

* `ImageStorage` - this tracks the amount of storage _per image_.
* `CustomerStorage` - this tracks the amount of storage _per space_.

The following 2 tables show what we store when an asset is ingested. The values vary depending on the `delivery-channel` and `origin`.

## Image / File

| Delivery-Channel | Origin        | IOP            | Storage                                                           |
| ---------------- | ------------- | -------------- | ----------------------------------------------------------------- |
| image            | non-optimised | `fast-higher`  | JP2 @ `/<id>`. `Storage`=X. `Thumbs`=Z                            |
| file             | non-optimised | `fast-higher`  | JPEG @ `/<id>/original`. `Storage`=Y. `Thumbs`=0                  |
| image,file       | non-optimised | `fast-higher`  | JP2 @ `/<id>`, JPEG @ `/<id>/original`. `Storage`=X+Y. `Thumbs`=Z |
| image            | s3-optimised  | `fast-higher`  | JP2 @ `/<id>`. `Storage`=X. `Thumbs`=Z                            |
| file             | s3-optimised  | `fast-higher`  | _no storage_                                                      |
| image,file       | s3-optimised  | `fast-higher`  | JP2 @ `/<id>`. `Storage`=X. `Thumbs`=Z                            |
| image            | non-optimised | `use-original` | JPEG @ `/<id>/original`. `Storage`=Y. `Thumbs`=Z                  |
| file             | non-optimised | `use-original` | JPEG @ `/<id>/original`. `Storage`=Y. `Thumbs`=0                  |
| image,file       | non-optimised | `use-original` | JPEG @ `/<id>/original`. `Storage`=Y. `Thumbs`=Z                  |
| image            | s3-optimised  | `use-original` | `Storage`=0. `Thumbs`=Z                                           |
| file             | s3-optimised  | `fast-higher`  | _no storage_                                                      |
| image,file       | s3-optimised  | `use-original` | `Storage`=0. `Thumbs`=Z                                           |

> Notes:
>
> * In "Storage" column: X is size of derivative, Y size of Origin + Z size of thumbs. `JP2` is used for DLCS generated and `JPEG` for origin file.
> * `fast-higher` is anything other than `use-original`

## Timebased / AV

| Delivery-Channel | Origin | Type  | Storage                                                     |
| ---------------- | ------ | ----- | ----------------------------------------------------------- |
| iiif-av          | http   | audio | mp3 @ `/<id>/full/*`. `Storage`=X                           |
| file             | http   | audio | raw @ `/<id>/original`. `Storage`=Y                         |
| iiif-av,file     | http   | audio | mp3 @ `/<id>/full/*`, wav @ `/<id>/original`. `Storage`=X+Y |
| iiif-av          | http   | video | mp4+webm @ `/<id>/full/*`. `Storage`=X                      |
| file             | http   | video | raw @ `/<id>/original`. `Storage`=Y                         |
| iiif-av,file     | http   | video | mp4+webm @ `/<id>`, wav @ `/<id>/original`. `Storage`=X+Y   |

> Notes:
> 
> * In "Storage" column: X is size of transcoded file(s), Y size of Origin. `raw` is origin file.
> * Audio transcodes to mp3, Video transcodes to mp4+webm.