# Engine

Engine is responsible for ingesting assets; either synchronously via an API call or by monitoring a queue.

## Ingestion

### API (Synchronous)

The engine has 2 routes for synchronous processing:

* `/asset-ingest` - Process incoming `IngestAssetRequest` - generating derivatives for asset delivery.
* `/image-ingest` - As above but takes `LegacyIngestEvent`, which is Deliverator notification model. The `LegacyIngestEvent` is converted to `IngestAssetRequest` and follows exact same process as above. _The intention is that this endpoint will be removed when Deliverator engine is retired_

### Queue (Asynchronous)

The engine also starts has a `BackgroundService`, `SqsListenerService` for asynchronous processing. 

This will subscribe to any queues that are configured in appSettings. The possible queues are:

* ImageQueue - Images queued for processing ("I" family). Process is as `/asset-ingest` with an alternative entrypoint. Can handle either `IngestAssetRequest` or `LegacyIngestEvent` messages.
* PriorityImageQueue - Exact same process as above, "priority" queue is used less often so will have less items queued.
* TimebasedQueue - Timebased assets queued for processing ("T" family). Not yet implemented.
* TranscodeCompleteQueue - Listens for notifcations from ElasticTranscoder and finishes processing of Timebased asset. Not yet implemented.

## Implementation Notes

### Customer Origin Strategy

A customer origin strategy specifies how an asset is to be fetched from origin. These are configured as a regex, a type and optional credentials - if an origin matches the regex then that strategy is used. The available strategies are:

* `default` - Fallback, an http request is used to fetch the origin.
* `basic-http` - Origin fetched via an http request with basic-authentication headers.
* `s3-ambient` - The fetch request is done using the AWS SDK, as such the DLCS must have access to the bucket. An `s3-ambient` origin can also be "optimised"; meaning that the assets are already tile-optimised. The DLCS will only generate derivatives for these and won't convert to a JPEG2000.
* `sftp` - Origin is fetched using a sftp _Not yet implemented_

### Ingesting

The engine ingests both Image ("I" family) and Timebased ("T" family) assets, File ("F"  family) assets are _not_ ingested.

The process for each is outlined below, the same process is used regardless of whether the request was initiated via an http request or a message from a queue:

#### Image

* Asset is downloaded from origin to local disk, using appropriate OriginStrategy.
* A request is made to image-processor sidecar ([Appetiser](https://github.com/dlcs/appetiser)). This will generate thumbnails in accordance with thumbnailPolicy and, optionally, generate a JPEG2000 in accordance with the imageOptimisationPolicy.
* Upload thumbnails to S3.
* Upload JPEG2000 to S3 if generated.
* Update the Asset database record with image dimensions, content-type from source (if provided) and mark as complete.
* Make a request to orchestrator for the new image info.json as this will trigger orchestration (optional - driven by config value)

#### Timebased

[AWS ElasticTranscoder](https://aws.amazon.com/elastictranscoder/) is used to transcode incoming media file to web optimised derivatives. 

* Asset is downloaded from origin to the ElasticTranscoder input S3 bucket.
  * If the origin-strategy is `s3-ambient` and customer override `"FullBucketAccess"` is true then the AWS SDK is used to copy between buckets (aka _direct copy_).
  * Else, the origin-strategy is used to download the AV file to local disk and then it is uploaded to S3 (aka _indirect copy_).
* An ElasticTranscoder job is created to transcode asset. Output type(s) are in accordance with imageOptimisationPolicy.
* Once complete ElasticTranscoder will raise an SQS notification.
* On receipt of this notification the processing is finalised:
  * AV output files are moved to correct S3 locations and permissions set.
  * Input file is removed.
  * Asset database record updated with dimensions and marked as complete.

## Configuration

There are a number of appsettings that control the behaviour of the application. These are in strongly typed to `EngineSettings` object and are split by prefix `ImageIngest:` or `TimebasedIngest:`

### `ImageIngest:`

| Key                           | Description                                                                                        | Default |
|-------------------------------|----------------------------------------------------------------------------------------------------|---------|
| `DestinationTemplate`         | Path template for where derivatives will be written to                                             |         |
| `ImageProcessorDelayMs`       | How long, in ms to delay calling image-processor after copying to shared disk.                     | `0`     |
| `ImageProcessorRoot`          | Root folder for use by Image-Processor sidecar                                                     |         |
| `ImageProcessorUrl`           | URI of downstream image/derivative processor (e.g. appetiser)                                      |         |
| `IncludeRegionInS3Uri`        | Whether to add region to s3:// URIs. Unofficial but required for backwards compat with deliverator | `false` |
| `OrchestratorBaseUrl`         | Base url for calling orchestrator                                                                  |         |
| `OrchestrateImageAfterIngest` | If true a request is made to Orchestrator to orchestrate image immediately after ingestion         | `true`  |
| `OrchestratorTimeoutMs`       | Timeout, in ms, to wait for calls to orchestrator                                                  | `5000`  |
| `ScratchRoot`                 | Root folder for engine, replaces `{root}` in templates                                             |         |
| `SourceTemplate`              | Path template for where assets downloaded to, for images should be accessible by image-processor   |         |
| `ThumbsTemplate`              | Path template for where thumbnail derivatives will generated to                                    |         |

### `TimebasedIngestSettings:`

| Key                  | Description                                                                                                   | Default |
|----------------------|---------------------------------------------------------------------------------------------------------------|---------|
| `PipelineName`       | The name of the ElasticTranscoder pipeline to use for transcoding AV files                                    |         |
| `ProcessingFolder`   | The root processing folder where temporary files are placed                                                   |         |
| `SourceTemplate`     | Template for location to download any assets to disk                                                          |         |
| `TranscoderMappings` | Dictionary mapping custom transcoder types to preset name (e.g. `"MyCustom Standard MP4": "Custom-mp4-128K"`) |         |

### `CustomerOverrides:`

This is a dictionary, keyed by the Id of the customer. The possible overrides are:

| Key                           | Description                                                                                    | Default |
|-------------------------------|------------------------------------------------------------------------------------------------|---------|
| `OrchestrateImageAfterIngest` | Overrides `ImageIngestSettings:OrchestrateImageAfterIngest`                                    |         |
| `NoStoragePolicyCheck`        | If `true` no storage limits are not verified for customer                                      |         |
| `FullBucketAccess`            | If `true`, indicates that S3 SDK can be used to copy timebased items directly bucket to bucket |         |