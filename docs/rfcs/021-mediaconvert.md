# AWS Elemental MediaConvert

As detailed in [ADR 0008](../adr/0008-et-replacement.md), use of ElasticTranscoder (ET) is being replaced with MediaConvert (MC).

The overall functionality of the system should remain unchanged - all API interactions will remain constant. 

This RFC outlines internal changes around how MC will be implemented. There are still a few unknowns with the MC API at this point so some of the below may be slightly altered once we're more familiar.

## Engine uses MediaConvert for transcoding

Rather than queue up a job with ET, use MC. This is the main change and driving force behind the changes.

The overall interactions should remain the same, ie
1. Engine transfers file from Origin to the S3 bucket MC will use for input. This happens either by downloading and uploading, or by direct bucket -> bucket transfer.
2. Engine makes an API request to initiate the transcode process.
3. Engine receives 'completed' notification, at which point it saves transcode information in `AssetApplicationMetadata` table, marks the Asset as completed and moves the Asset to the relevant location, ready to be streamed.

Some points relevant to each of the above steps:
* We append some application specific metadata to the ET API request, this is used to link initiation and completion events. If MC API doesn't support arbitrary metadata then we will need to store this elsewhere.
* When initiating transcode operation we write the ET Id to S3 for powering the `/customers/{customerId}/spaces/{spaceId}/images/{imageId}/metadata` API endpoint. We will need to persist this behaviour with MC.

## `/metadata` endpoint

As mentioned above - the `/customers/{customerId}/spaces/{spaceId}/images/{imageId}/metadata` endpoint will need to be supported.

To fully realise the request we read the jobId saved on ingest and make an API request to ET API. All `/metadata` requests for jobs ingested via ET will cease to work once the service is removed. We should detect that the job was for ET rather than MC and return a 404 with appropriate message.

Initially I thought a [410 | Gone](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status/410) status would be more appropriate but decided against 404 due to this note on linked page:

> If server owners don't know whether this condition is temporary or permanent, a 404 status code should be used instead.

If an asset had been transcoded via ET but was subsequently reingested it would have metadata to return, so we aren't sure whether it is temporary or permanent.

### Job Identification

To maintain backwards compatibility with Deliverator, the job details are currently stored as a blob of XML with format :`<JobInProgress><ElasticTranscoderJob>{elasticTranscoderJobId}</ElasticTranscoderJob></JobInProgress>`.

I suggest we alter MC metadata to use JSON and detect whether the format is XML or JSON to determine if it's ET or MC. Alternatively we use `<JobInProgress><MediaConvertJob>` if it is easier.

## Preset Management

[RFC 015](./015-iiif-av-delivery-channel-settings.md) outlines how AV DeliveryChannel settings are configured. 

The result of which was to store presets in format `<channel>-<extension>-<quality>` (e.g. `"video-mp4-480p"`), which is then mapped to an ET preset (e.g. `"System preset: Generic 480p 4:3"`). The preset string is also mapped to `TimeBasedPolicy` class, which has `channel`, and `extension` properties. 

The `TimeBasedPolicy` object is used to identify the extension to be used for the generated manifest (e.g. in above example it'll be stored as `*.mp4`).

This format is useful but encodes more information into the friendly name than was initially envisaged. Switching to MC could give us the opportunity to support friendly names that can have any format (e.g. `"fastest"`, `"premium"` or `"exhibition-quality"`). Either the stored MC template name can also include the desired extension, or the template can be used to lookup MC metadata about that present to determine the most appropriate extension. Which way is best will depend on whether the extension is available from MC API.

## File Extension and Cleanup

Related to the above point, the location we store the transcode at uses the extension derived from the 'friendly' preset name. The location stored in `AssetApplicationMetadata` and the location used by the cleanup handler is derived from ET API response. This has lead to some inconsistencies, see [#981](https://github.com/dlcs/protagonist/issues/981) for details.

We should endeavour to make this consistent, using one method for all extension identification.

## Storage and `/iiif-av/` paths

The final output location we use to store audio and video files uses a set format:
* `{asset}/full/max/default.{extension}` for audio
* `{asset}/full/full/max/max/0/default.{extension}` for video

These are identical to the `/iiif-av/` path called by a consumer. The drawback to this is that we can only have 1 transcode with a set extension, we couldn't have a 128kb mp3 and a 256kb mp3 as they would both be stored at `*.mp3` (see [#970](https://github.com/dlcs/protagonist/issues/970) for more details).

Given we store transcode details in `AssetApplicationMetadata` table, e.g.

```json
[
    {
        "d": 92000,
        "l": "s3://dlcs-storage/1/2/example-audio-with-multiple-transcodes/full/max/default.mp3",
        "n": "System preset: Audio MP3 - 128k",
        "ex": "mp3",
        "mt": "audio/mp3"
    }
]
```

and we output paths on single-item and NQ manifests (see [RFC 020](./020-non-image-iiif.md)) we could use alternative paths for storing and proxying - they don't need to be 1:1 mapping. All that we would need to do is make a lookup to the DB to determine the actual storage location for an incoming request. The `/iiif-av/` path would need to be thought through so that we can accurately map.

## Engine http endpoints

The Engine serves as a single source of truth for transcode mappings. The following endpoints are present to allow other services to access these:

### `/av-presets/`

This is used by the `AssetUpdateHandler` to determine which extensions should exist for the updated asset (which is then compared with the actual keys - any diffs are removed). 

> [!NOTE]
> The `AssetApplicationMetadata` table stores transcode locations for all AV files transcoded since [v1.7.0](https://github.com/dlcs/protagonist/releases/tag/v1.7.0) was released. We could use this but have opted not to do this elsewhere as multiple quick ingest requests could lead to confusion about the initial state of an asset prior to any write operations as we don't have guaranteed ordering of messages.

### `/allowed-av/`

This returns a list of AV policies and is used by the API to validate incoming requests.

### Suggestion

I suggest we keep the functionality as-is but move the endpoints to their own controller (they're currently in the `IngestController` but are not related to any ingest actions).

* `/av-presets/` becomes `/av/presets`
* `/allowed-av/` becomes `/av/policies`