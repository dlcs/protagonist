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

For `default` and `basic-http` the IP address that the origin resolves to is checked before connecting, and the connection refused if it falls in a blocked range. The check happens per connection, so every hop of a redirect chain is verified.

Loopback (`127.0.0.0/8`, `::1`), link-local (`169.254.0.0/16`, `fe80::/10`), unique-local (`fc00::/7`) and unspecified (`0.0.0.0/8`, `::`) are blocked by default - link-local and unique-local cover the cloud instance-metadata addresses (`169.254.169.254`, `fd00:ec2::254`), and connecting to an unspecified address reaches loopback. Further ranges can be blocked via `OriginStrategy:BlockedIpRanges`. If a host resolves to a mix of allowed and blocked addresses then the connection is refused outright.

`OriginStrategy:AllowedIpRanges` permits addresses that would otherwise be blocked, for local development and for deployments whose origins legitimately sit on internal addresses. An allowed range wins. The instance-metadata addresses are the one exception and can never be allowed. For running Engine outside Docker against an origin served from the host:

```json
"OriginStrategy": {
  "AllowedIpRanges": [ "127.0.0.0/8", "::1/128" ]
}
```

> [!Note]
> The HTTP client for `default` and `basic-http` does not use a proxy, even if `HTTP_PROXY` / `HTTPS_PROXY` are set in the environment as a proxy would resolve the origin host on our behalf and the address checks would only ever see the proxy.

### Ingesting

The process for each asset delivery-channel is outlined below, the same process is used regardless of whether the request was initiated via an http request or a message from a queue:

#### Image (iiif-img channel)

* Asset is downloaded from origin to local disk, using appropriate origin-strategy.
* A request is made to image-processor sidecar ([Appetiser](https://github.com/dlcs/appetiser)). This will generate a combination of thumbnails a JPEG2000 in accordance with the deliveryChannelPolicy.
* Upload thumbnails to S3.
* Handle image-server source image (see [ADR#0005](https://github.com/dlcs/protagonist/blob/develop/docs/adr/0005-optimised-origin.md#tile-ready))
* Update the "Images" database record with image dimensions, "ImageLocation" with where this is stored, "ImageStorage" with size of bytes stored and mark as complete.
* Make a request to orchestrator for the new image info.json as this will trigger orchestration (optional - driven by config value).
* Delete any existing info.json files for this asset.

#### Timebased (iiif-av channel)

[AWS Elemental MediaConvert](https://aws.amazon.com/mediaconvert/) is used to transcode incoming media file to web optimised derivatives. 

* Asset is downloaded from origin to the MediaConvert input S3 bucket.
  * If the origin-strategy is `s3-ambient` and optimised then the AWS SDK is used to copy between buckets (aka _direct copy_).
  * Else, the origin-strategy is used to download the AV file to local disk and then it is uploaded to S3 (aka _indirect copy_).
* A MediaConvert job is created to transcode asset. Output type(s) are in accordance with deliveryChannelPolicy.
* Once complete MediaConvert puts message on SQS queue.
* On receipt of this notification the processing is finalised:
  * AV output files are moved to correct S3 locations and permissions set.
  * Input file is removed.
  * "Images" database record updated with dimensions and marked as complete, "ImageStorage" is updated with size of bytes stored

A list of transcode policies supported by Engine (as a JSON string array) can be retrieved the `/av/allowed` route.

#### File (file channel)

* If asset is stored at optimised origin this is a no-op (we will server from origin). Else,
* Asset is copied from origin to S3 bucket, this will be direct or indirect, as with Timebased.

## Configuration

There are a number of appsettings that control the behaviour of the application. 

These are in strongly typed to `EngineSettings` object and are split by prefix below:

| Key                | Description                                                 | Default |
| ------------------ | ----------------------------------------------------------- | ------- |
| `DownloadTemplate` | Template for download location for temporary working assets |         |
| `MaxWidth`         | System default `maxWidth` property                          | 5000    |

### `ImageIngest:`

| Key                           | Description                                                                                        | Default                                              |
| ----------------------------- | -------------------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| `CloseBracketReplacement`     | The character to use when replacing an closing bracket character when saving to disk               | `_`                                                  |
| `DefaultThumbs`               | A list of thumbnails that will be added to every asset regardless of the thumbnail policy          | `["!100,100", "!200,200", "!400,400", "!1024,1024"]` |
| `DestinationTemplate`         | Path template for where derivatives will be written to                                             |                                                      |
| `ImageProcessorDelayMs`       | How long, in ms to delay calling image-processor after copying to shared disk.                     | `0`                                                  |
| `ImageProcessorTimeoutMs`     | Timeout, in ms, for requests to image-processor                                                    | `300000`                                             |
| `ImageProcessorRoot`          | Root folder for use by Image-Processor sidecar                                                     |                                                      |
| `ImageProcessorUrl`           | URI of downstream image/derivative processor (e.g. appetiser)                                      |                                                      |
| `IncludeRegionInS3Uri`        | Whether to add region to s3:// URIs. Unofficial but required for backwards compat with deliverator | `false`                                              |
| `OpenBracketReplacement`      | The character to use when replacing an open bracket character when saving to disk                  | `_`                                                  |
| `OrchestratorBaseUrl`         | Base url for calling orchestrator                                                                  |                                                      |
| `OrchestrateImageAfterIngest` | If true a request is made to Orchestrator to orchestrate image immediately after ingestion         | `true`                                               |
| `OrchestratorTimeoutMs`       | Timeout, in ms, to wait for calls to orchestrator                                                  | `5000`                                               |
| `ScratchRoot`                 | Root folder for engine, replaces `{root}` in templates                                             |                                                      |
| `SourceTemplate`              | Path template for where assets downloaded to, for images should be accessible by image-processor   |                                                      |
| `ThumbsTemplate`              | Path template for where thumbnail derivatives will generated to                                    |                                                      |

### `OriginStrategy:`

| Key               | Description                                                                                                                                                                  | Default |
| ----------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------- |
| `BlockedIpRanges` | IP ranges, in CIDR notation, that an origin is forbidden from resolving to. Loopback, link-local, unique-local + unspecified are blocked by default.                         | `[]`    |
| `AllowedIpRanges` | IP ranges, in CIDR notation, that an origin may resolve to despite the above. Overrides both default and configured blocks; instance-metadata addresses are never be allowed | `[]`    |

### `AWS:AssumeRole`

Controls whether AWS clients are scoped to the customer currently being ingested. When enabled, Engine assumes a role
per customer, tagging the STS session with that customer. IAM policies can then restrict access by customer via
`aws:PrincipalTag/Customer` - without this, ambient task-role credentials are used and any customer can reach any
asset the task has access to.

This applies to S3, SNS and MediaConvert clients. SQS is unaffected, the queue listener polls before any customer is
known. If enabled, any AWS request made outside of an ingest will fail - customer-scoped clients deliberately fail
closed rather than falling back to the ambient role.

This is ignored, and treated as disabled, if LocalStack is in use.

| Key                 | Description                                                                                | Default      |
| ------------------- | ------------------------------------------------------------------------------------------ | ------------ |
| `Enabled`           | Whether AWS clients are scoped to the current customer                                     | `false`      |
| `RoleArn`           | Arn of role to assume, typically the Engine task-role itself. Required if `Enabled`        |              |
| `DurationSeconds`   | How long an assumed session is valid for. 3600 is the maximum for a chained role           | `3600`       |
| `TagKey`            | Key of the session tag that carries the customer id                                        | `Customer`   |
| `SessionNamePrefix` | Prefix for the role session name, customer id is appended                                  | `customer-`  |
| `MaxCachedClients`  | Maximum number of entries held in each of the credential and client caches                 | `100`        |
| `CacheIdleMinutes`  | How long an unused cached credential/client is kept for                                    | `60`         |

The role being assumed must trust itself and allow session tagging. See
[the DLCS adjunct access-control RFC](https://github.com/dlcs/protagonist) for sample trust policies and the
corresponding bucket policies.

### `AWS:Transcode`


| Key                       | Description                                                                                                      | Default |
| ------------------------- | ---------------------------------------------------------------------------------------------------------------- | ------- |
| `QueueName`               | Name of the MediaConvert queue to use                                                                            |         |
| `RoleArn`                 | Arn of role to use for MediaConvert queue to use                                                                 |         |
| `DeliveryChannelMappings` | Mapping values for policy-data name to preset+extension. e.g. `{ "audio-mp3" : "SystemPreset_foo_bar_q1\|wav" }` |         |

### `CustomerOverrides:`

This is a dictionary, keyed by the Id of the customer. The possible overrides are:

| Key                           | Description                                                 | Default |
| ----------------------------- | ----------------------------------------------------------- | ------- |
| `OrchestrateImageAfterIngest` | Overrides `ImageIngestSettings:OrchestrateImageAfterIngest` |         |
| `NoStoragePolicyCheck`        | If `true` no storage limits are not verified for customer   |         |

Any "Template" settings support the following replacements (using `1/2/foo-bar-baz` as sample image)

* `{root}` - replacement dependant on value passed to method
* `{customer}` - uses customer element of AssetId (`1`)
* `{space}` - uses space element of AssetId (`2`)
* `{image}` - uses image element of AssetId (`foo-bar-baz`)
* `{image-dir}` - uses image element of AssetId converted to [PairTree](https://ocfl.io/1.0/implementation-notes/#storage-root-hierarchy) (`fo/o-/ba/r-/foo-bar-baz`)