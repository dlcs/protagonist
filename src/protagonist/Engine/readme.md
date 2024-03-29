# Engine

Engine is responsible for ingesting assets; either synchronously via an API call or by monitoring a queue.

## Ingestion

### API (Synchronous)

For synchronous processing, the engine takes incoming`IngestAssetRequest` at `/asset-ingest`, generating derivatives for asset delivery.

### Queue (Asynchronous)

The engine also starts has a `BackgroundService`, `SqsListenerService` for asynchronous processing. 

This will subscribe to any queues that are configured in appSettings. The possible queues are:

* ImageQueue - Images queued for processing (assets with "iiif-img" delivery-channel). Process is as `/asset-ingest` with an alternative entrypoint. Can handle either `IngestAssetRequest` or `LegacyIngestEvent` messages.
* PriorityImageQueue - Exact same process as above, "priority" queue is used less often so will have less items queued.
* TimebasedQueue - Timebased assets queued for processing (assets with "iiif-av" delivery-channel).
* FileQueue - Any assets queued for processing assets with "file" delivery-channel.
* TranscodeCompleteQueue - Listens for notifcations from ElasticTranscoder and finishes processing of Timebased asset.

> Note that ImageQueue and TimebasedQueue could also receive assets with "file" delivery-channel.

## Implementation Notes

### Customer Origin Strategy

A customer origin strategy specifies how an asset is to be fetched from origin. These are configured as a regex, a type and optional credentials - if an origin matches the regex then that strategy is used. The available strategies are:

* `default` - Fallback, an http request is used to fetch the origin.
* `basic-http` - Origin fetched via an http request with basic-authentication headers.
* `s3-ambient` - The fetch request is done using the AWS SDK, as such the DLCS must have access to the bucket. An `s3-ambient` origin can also be "optimised"; meaning that the DLCS has permissino to access it via SDK.
* `sftp` - Origin is fetched using a sftp _Not yet implemented_

### Ingesting

The process for each asset delivery-channel is outlined below, the same process is used regardless of whether the request was initiated via an http request or a message from a queue:

#### Image (iiif-img channel)

* Asset is downloaded from origin to local disk, using appropriate origin-strategy.
* A request is made to image-processor sidecar ([Appetiser](https://github.com/dlcs/appetiser)). This will generate thumbnails in accordance with thumbnailPolicy and, optionally, generate a JPEG2000 in accordance with the imageOptimisationPolicy.
* Upload thumbnails to S3.
* Handle image-server source image (see [ADR#0005](https://github.com/dlcs/protagonist/blob/develop/docs/adr/0005-optimised-origin.md#tile-ready))
* Update the "Images" database record with image dimensions, "ImageLocation" with where this is stored, "ImageStorage" with size of bytes stored and mark as complete.
* Make a request to orchestrator for the new image info.json as this will trigger orchestration (optional - driven by config value).
* Delete any existing info.json files for this asset.

#### Timebased (iiif-av channel)

[AWS ElasticTranscoder](https://aws.amazon.com/elastictranscoder/) is used to transcode incoming media file to web optimised derivatives. 

* Asset is downloaded from origin to the ElasticTranscoder input S3 bucket.
  * If the origin-strategy is `s3-ambient` and optimised then the AWS SDK is used to copy between buckets (aka _direct copy_).
  * Else, the origin-strategy is used to download the AV file to local disk and then it is uploaded to S3 (aka _indirect copy_).
* An ElasticTranscoder job is created to transcode asset. Output type(s) are in accordance with imageOptimisationPolicy.
* Once complete ElasticTranscoder will raise an SQS notification.
* On receipt of this notification the processing is finalised:
  * AV output files are moved to correct S3 locations and permissions set.
  * Input file is removed.
  * "Images" database record updated with dimensions and marked as complete, "ImageStorage" is updated with size of bytes stored

A list of transcode policies supported by Engine (as a JSON string array) can be retrieved the `/allowed-av` route.

#### File (file channel)

* If asset is stored at optimised origin this is a no-op (we will server from origin). Else,
* Asset is copied from origin to S3 bucket, this will be direct or indirect, as with Timebased.

## Configuration

There are a number of appsettings that control the behaviour of the application. 

These are in strongly typed to `EngineSettings` object and are split by prefix below:

| Key                | Description                                                 | Default |
| ------------------ | ----------------------------------------------------------- | ------- |
| `DownloadTemplate` | Template for download location for temporary working assets |         |

### `ImageIngest:`

| Key                           | Description                                                                                        | Default  |
| ----------------------------- | -------------------------------------------------------------------------------------------------- | -------- |
| `DestinationTemplate`         | Path template for where derivatives will be written to                                             |          |
| `ImageProcessorDelayMs`       | How long, in ms to delay calling image-processor after copying to shared disk.                     | `0`      |
| `ImageProcessorTimeoutMs`     | Timeout, in ms, for requests to image-processor                                                    | `300000` |
| `ImageProcessorRoot`          | Root folder for use by Image-Processor sidecar                                                     |          |
| `ImageProcessorUrl`           | URI of downstream image/derivative processor (e.g. appetiser)                                      |          |
| `IncludeRegionInS3Uri`        | Whether to add region to s3:// URIs. Unofficial but required for backwards compat with deliverator | `false`  |
| `OrchestratorBaseUrl`         | Base url for calling orchestrator                                                                  |          |
| `OrchestrateImageAfterIngest` | If true a request is made to Orchestrator to orchestrate image immediately after ingestion         | `true`   |
| `OrchestratorTimeoutMs`       | Timeout, in ms, to wait for calls to orchestrator                                                  | `5000`   |
| `ScratchRoot`                 | Root folder for engine, replaces `{root}` in templates                                             |          |
| `SourceTemplate`              | Path template for where assets downloaded to, for images should be accessible by image-processor   |          |
| `ThumbsTemplate`              | Path template for where thumbnail derivatives will generated to                                    |          |

### `TimebasedIngestSettings:`

| Key                  | Description                                                                                                   | Default |
| -------------------- | ------------------------------------------------------------------------------------------------------------- | ------- |
| `PipelineName`       | The name of the ElasticTranscoder pipeline to use for transcoding AV files                                    |         |
| `TranscoderMappings` | Dictionary mapping custom transcoder types to preset name (e.g. `"MyCustom Standard MP4": "Custom-mp4-128K"`) |         |

### `CustomerOverrides:`

This is a dictionary, keyed by the Id of the customer. The possible overrides are:

| Key                           | Description                                                                                    | Default |
| ----------------------------- | ---------------------------------------------------------------------------------------------- | ------- |
| `OrchestrateImageAfterIngest` | Overrides `ImageIngestSettings:OrchestrateImageAfterIngest`                                    |         |
| `NoStoragePolicyCheck`        | If `true` no storage limits are not verified for customer                                      |         |

Any "Template" settings support the following replacements (using `1/2/foo-bar-baz` as sample image)

* `{root}` - replacement dependant on value passed to method
* `{customer}` - uses customer element of AssetId (`1`)
* `{space}` - uses space element of AssetId (`2`)
* `{image}` - uses image element of AssetId (`foo-bar-baz`)
* `{image-dir}` - uses image element of AssetId converted to [PairTree](https://ocfl.io/1.0/implementation-notes/#storage-root-hierarchy) (`fo/o-/ba/r-/foo-bar-baz`)