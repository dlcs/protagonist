# Storage Keys

The DLCS uses a number of S3 keys in various buckets to store assets. These generally follow a known pattern and have meanings. All of these locations should be captured in `S3StorageKeyGenerator` class but are outlined below with some narrative where relevant. 

> Note:
> The default 'storage-key' is `$"{customer}/{space}/{assetKey}"`.
> The various bucketnames below equate to those in `S3Settings`

| Name                        | Format                                                                    | Example                                                | Description                                                                                       |
| --------------------------- | ------------------------------------------------------------------------- | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------- |
| Storage                     | `"{StorageBucket}/{storage-key}"`                                         | `dlcs-storage/1/2/foo`                                 | Default location where generated derivatives are stored                                           |
| Storage Original            | `"{StorageBucket}/{storage-key}/original"`                                | `dlcs-storage/1/2/foo/original`                        | Where direct-copy of origin is stored. For `/file/` delivery or images with `use-original` policy |
| InfoJson                    | `"{StorageBucket}/{storage-key}/info/{image-server}/{version}/info.json"` | `dlcs-storage/1/2/foo/info/cantaloupe/v3/info.json`    | Location where pregenerated info.json stored                                                      |
| Audio output                | `"{StorageBucket}/{storage-key}/full/max/default.{extension}"`            | `dlcs-storage/1/2/foo/full/max/default.mp3`            | Location where transcoded audio stored                                                            |
| Video output                | `"{StorageBucket}/{storage-key}/full/full/max/max/0/default.{extension}"` | `dlcs-storage/1/2/foo/full/full/max/max/0/default.mp4` | Location where transcoded video stored                                                            |
| Timebased Metadata          | `"{StorageBucket}/{storage-key}/metadata"`                                | `dlcs-storage/1/2/foo/metadata`                        | XML blob storing ElasticTranscoder JobId                                                          |
| Thumbnail                   | `"{ThumbsBucket}/{storage-key}/{access}/{longestEdge}.jpg"`               | `dlcs-thumbs/1/2/foo/open/100.jpg`                     | Location of specific thumbnail                                                                    |
| Legacy Thumbnail            | `"{ThumbsBucket}/{storage-key}/full/{w},{h}/0/default.jpg"`               | `dlcs-thumbs/1/2/foo/full/100,200/0/default.jpg`       | Location of specific thumbnail using legacy layout                                                |
| Thumbnail Sizes             | `"{ThumbsBucket}/{storage-key}/s.json"`                                   | `dlcs-thumbs/1/2/foo/s.json`                           | JSON blob storing known thumbnails                                                                |
| Largest Thumbnail           | `"{ThumbsBucket}/{storage-key}/low.jpg"`                                  | `dlcs-thumbs/1/2/foo/low.jpg`                          | The location of the largest generated thumbnail                                                   |
| Thumbnail Root              | `"{ThumbsBucket}/{storage-key}/"`                                         | `dlcs-thumbs/1/2/foo/`                                 | Root key where thumbnails will reside                                                             |
| Output Location             | `"{OutputBucket}/{storage-key}/"`                                         | `dlcs-output/1/2/foo/`                                 | Root key where DLCS 'output' is stored (e.g. projected NQ to PDF or Zip)                          |
| Origin Location             | `"{OriginBucket}/{storage-key}"`                                          | `dlcs-origin/1/2/foo`                                  | Location where directly uploaded bytes are stored                                                 |
| Transient Images            | `"{StorageBucket}/transient/{storage-key}"`                               | `dlcs-thumbs/1/2/foo/open/100.jpg`                     | Location of transient images, that will be cleaned up by lifecycle policies                       |


## Timebased

There are a number of other locations used for ElasticTranscoder interactions but these are transient and not included.

## Output Location

Note that for `Output Location` the exact key is determined by the NamedQuery projector.

These can differ per projection type, but the defaults are:

* PDF: `"{customer}/pdf/{queryname}/{args}"`, e.g. `10/pdf/pdf-named-query/10/5/a-string.pdf
* Zip: `"{customer}/zip/{queryname}/{args}"`, e.g. `10/zip/zip-named-query/10/5/a-string.zip

 `args` are all NQ arguments delimited by a `/`.

 The control-file for each item is stored at the same key with `.json` appended.
