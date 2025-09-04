# AWS Elemental MediaConvert

As detailed in [ADR 0008](../adr/0008-et-replacement.md), use of ElasticTranscoder (ET) is being replaced with MediaConvert (MC).

The overall functionality of the system should remain unchanged - all API interactions will remain constant. 

## Engine uses MediaConvert for transcoding

Rather than queue jobs with ET, use MC. This is the driving force behind all changes here.

The overall interactions should remain the same, ie
1. Engine transfers file from Origin to the S3 bucket MC will use for input. This happens either by downloading and uploading, or by direct bucket -> bucket transfer.
2. Engine makes an API request to initiate the transcode process.
3. Engine polls a queue for 'completed' notifications, at which point it saves transcode information in `AssetApplicationMetadata` table, marks the Asset as completed and moves the Asset to the relevant storage location, ready to be streamed.

Some points relevant to each of the above steps:
* We append application specific metadata to the ET API request, this is used to link initiation and completion events. MC API has similar arbitrary metadata concept.
* When initiating a transcode operation we write the ET Id to S3 for powering the `/customers/{customerId}/spaces/{spaceId}/images/{imageId}/metadata` API endpoint. We will need to persist this behaviour with MC.

## Internal Models

Within the `DLCS.AWS.ElasticTranscoder.Models` namespace we have a number of models, e.g. `TranscoderJob`, `TranscoderInput`, `TranscoderOutput` etc. These are heavily based on ET models but are an internal representation, added as a layer of indirection should the underlying service change. We may need to alter them to accommodate the MC models but where possible we should endeavour to use them as it will reduce downstream changes.

These models should also be moved to an alternative namespace, e.g. `DLCS.AWS.Transcoding.Models`, as they're not specifically related to the ET service.

## `/metadata` endpoint

As mentioned above - the `/customers/{customerId}/spaces/{spaceId}/images/{imageId}/metadata` endpoint will need to be supported.

To fully realise the request we read the jobId saved on ingest from S3 and make ET [`read-job`](https://docs.aws.amazon.com/cli/latest/reference/elastictranscoder/read-job.html) request, the equivalent MC call is [`get-job`](https://docs.aws.amazon.com/it_it/cli/latest/reference/mediaconvert/get-job.html). The ultimate return object is the above `TranscoderJob`, which shouldn't be tied to any particular transcoding service.

All `/metadata` requests for jobs ingested via ET will cease to work after the service is removed. We should detect that the job was for ET rather than MC and return a 404 with appropriate message. Initially I thought a [410 | Gone](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status/410) status would be more appropriate but decided to use a 404 due to this note on linked page:

> If server owners don't know whether this condition is temporary or permanent, a 404 status code should be used instead.

If an asset had been transcoded via ET and subsequently reingested via MC it would have metadata to return, so we aren't sure whether it is temporary or permanent.

### Job Identification

To maintain backwards compatibility with Deliverator, the job details are currently stored as a blob of XML with format :`<JobInProgress><ElasticTranscoderJob>{elasticTranscoderJobId}</ElasticTranscoderJob></JobInProgress>`.

I suggest we either:
* Store MC metadata as JSON and identify type via file format, or
* Continue to use XML but use an alternative node name, e.g. `<JobInProgress><MediaConvertJob>` 

## Preset Management

[RFC 015](./015-iiif-av-delivery-channel-settings.md) outlines how AV DeliveryChannel settings are configured. 

The result of which was to store presets in format `<channel>-<extension>-<quality>` (e.g. `"video-mp4-480p"`), which is then mapped to an ET preset (e.g. `"System preset: Generic 480p 4:3"`). The preset string is parsed to `TimeBasedPolicy` class, which has `channel`, and `extension` properties. This object is used to identify the extension to be used for the generated transcodes (e.g. in above example it'll be stored as `*.mp4`).

The above format is useful but encodes more information into the friendly name than was initially envisaged. Switching to MC gives us the opportunity to support friendly names that can have any format (e.g. `"fastest"`, `"premium"` or `"exhibition-quality"`).

If no extension is specified for the output of an MC job, MC will automatically set it based on the output container or codec. This is useful behaviour but to fully leverage this we would need to have the same logic within Engine as the MC API as we cannot programmatically determine which extension will be used/was used. MC `get-preset` response doesn't contain the extension that _would_ be used and the `get-job` response doesn't contain which extension _was_ used so we need to track that elsewhere.

Proposal is that we continue to store the presets as key value pairs, where:
* key is a string without any meaning (e.g. `"fastest"` or `"video-mp4-480p"`).
* value is in format `{preset}|{extension}` (e.g. `"System-Generic_Hd_Mp4_Av1_Aac_16x9_1280x720p_24Hz_1Mbps_Qvbr_Vq6|mp4"`)
  * `|` was chosen as separator character as it's invalid for a preset name

### File Extension consistencies

Related to the above point, the final S3 storage key of the transcode uses the extension derived from the 'friendly' preset name. The location stored in `AssetApplicationMetadata` and the location used by the cleanup handler is derived from ET API response. This has lead to some inconsistencies as the expected key can differ, see [#981](https://github.com/dlcs/protagonist/issues/981) for details.

By implementing the above suggestion we will be using a consistent method for all extension identification.

## Storage and `/iiif-av/` paths

The final storage key we use to store audio and video files uses a set format:
* `{asset}/full/max/default.{extension}` for audio
* `{asset}/full/full/max/max/0/default.{extension}` for video

These are identical to the `/iiif-av/` path called by a consumer. The drawback to this is that we can only have 1 transcode per extension, we couldn't have a 128kb mp3 and a 256kb mp3 as they would both be stored at `*.mp3` (see [#970](https://github.com/dlcs/protagonist/issues/970) for more details).

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

> [!NOTE]
> This should be a future enhancement and not implemented at the same time as initial MC implementation

## Engine http endpoints

The Engine serves as a single source of truth for transcode mappings. The following endpoints are present to allow other services to access these:

### `/av-presets/`

This is used by the `AssetUpdateHandler` to determine which extensions should exist for the updated asset (which is then compared with the actual keys - any diffs are removed). 

> [!NOTE]
> The `AssetApplicationMetadata` table stores transcode locations for all AV files transcoded since [v1.7.0](https://github.com/dlcs/protagonist/releases/tag/v1.7.0) was released. We could use this but have opted not to do this elsewhere as multiple quick ingest requests could lead to confusion about the initial state of an asset prior to any write operations as we don't have guaranteed ordering of messages.

### `/allowed-av/`

This returns a list of AV policies and is used by the API to validate incoming requests.

### Suggestion

I suggest we keep the functionality as-is but move the endpoints to their own controller (they're currently in the `IngestController` but are not related to any ingest actions) and update their paths:

* `/av-presets/` becomes `/av/presets`
* `/allowed-av/` becomes `/av/policies`

## Appendix 

Appendix contains some implementation notes around using MC

### Terminology

MC has a lot of the same concepts as ET, albeit it's a lot more flexible. Calling out the key concepts below:

* A `queue` organises the processing of jobs. Similar to ET but queues are not directly tied to one input or output source. There are on-demand or reserved queues, I expect we'll always use on-demand.
* A `job` does the actual work of transcoding (converts input(s) -> output(s)). 
* An output `preset` is a saved group of encoding settings for a single output, similar to ET presets.
* A job `template` is a preconfigured template that can be used to generate a job. It contains a number of settings, allowing those that are likely to change (e.g. IAM role, input path, name, metadata etc) to be overridden.
* `input` - a single input to transcode. Each job can have 1:n inputs
* `output` - a single output of transcode operation, this can be a single file or a set of files (e.g. for adaptive bitrate). Useful properties here are `extension` and `name-modifier`. The former is defaulted as detailed above, the latter allows multiple outputs in the same group to differentiate each other
* `outputGroup` - a grouping of outputs. Each job can have 1:n `outputGroups`, each containing 1:n `outputs` (e.g. group 1 could be video with subtitles and captions in English, group 2 in French. Or different adaptive bitrates per group etc).

#### Output Examples

For clarity, example `output` and `outputGroup` configuration below. 

If the `outputGroup` had destination of `s3://my-bucket/key` and we had 3 `outputs` we can use variations of `extension` and `name-modifier` to help with issues around transcodes sharing extensions (see [#970](https://github.com/dlcs/protagonist/issues/970)).

* Output1: extension=mp4, name-modifier=_128k. Result would be `s3://my-bucket/key_128k.mp4`
* Output2: extension=mp4, name-modifier=_192k. Result would be `s3://my-bucket/key_192k.mp4`
* Output3: extension=m4a. Result would be `s3://my-bucket/key.m4a`

### Implementation

We should have a single `queue` per Protagonist instance. Queues can process multiple jobs concurrently up to a limit (currently 2000 across all queues in an account - we will likely never hit these).

Each ingest operation should be a single `job` with a single `outputGroup`. This `outputGroup` will contain 1:n `outputs`, each one being for a specific preset. Each `output` will hardcode the `extension` property (as mentioned above, if MC sets the extension it's unavailable in job response payloads).

Using `templates` is unnecessary for our use case - one option would be to link a DeliveryChannel policy to a `template` rather than a `preset` but this would mean that multiple transcodes would result in multiple MC jobs, which we'd need to track and link together. `Template` use would also come with overhead of managing them in IaC, see below for more on this.

### Preset Equivalents

By default Protagonist uses 2 ET presets:

* `System preset: Generic 720p` for video. All of the `category=Generic-HD` system presets in MC are roughly equivalent, I still need to identify the exact replacement. For this spike I've used `System-Generic_Hd_Mp4_Av1_Aac_16x9_1280x720p_24Hz_1Mbps_Qvbr_Vq6`, which is the lowest quality HD preset but this has more noise than ET. There is an `MP4` category but these are all 4K.
* `System preset: Audio MP3 - 128k` for audio. There are no audio only system presets for MC. I have manually create a preset based on the ET present, this can be exported and imported into relevant AWS estates.

### IaC

Terraform support for MC resources is minimal, currently only [`queue`](https://registry.terraform.io/providers/hashicorp/aws/latest/docs/resources/media_convert_queue) is supported. There are github issues dating back to 2019 to add support for this, see [terraform-provider-aws#11186](https://github.com/hashicorp/terraform-provider-aws/issues/11186), [terraform-provider-aws#11189](https://github.com/hashicorp/terraform-provider-aws/issues/11189) [terraform-provider-aws#11190](https://github.com/hashicorp/terraform-provider-aws/issues/11190).

I suspect the lack of support is related to this warning from [AWS docs](https://docs.aws.amazon.com/mediaconvert/latest/ug/example-job-settings.html)

> We recommend that you use the MediaConvert console to generate your production JSON job specification.
>
> Your job specification must conform to validation by the transcoding engine. The transcoding engine validations represent complex dependencies among groups of settings and dependencies between your transcoding settings and properties of your input files. The MediaConvert console functions as an interactive job builder to make it easy to create valid job JSON specifications. You can use job templates and output presets to get started quickly.

#### Implementation

We will have IaC for `queue`, IAM and notification/logging. MC is more flexibly in how it raises notifications but I suggest we stick replicating what ET did for now (ie place a message on SQS queue on completion).

The only other resource we need to create is the audio preset. Given there are no TF resources for this I suggest a combination of [terraform-data](https://developer.hashicorp.com/terraform/language/resources/terraform-data) and [local-exec](https://developer.hashicorp.com/terraform/language/resources/provisioners/local-exec) create from exported preset JSON. 

All MC related TF can be managed by a module.

## Future Improvements

Noting down some possible future enhancements based on MC functionality:

* In multi-tenant environments we could have a queue per customer.
* Option to use priority for jobs, this can allow items to skip to the front of the queue.
* Read direct from http origin, or use ambient s3-origin to fetch directly from S3 origin.
* As noted above - we can disconnect the advertised request path from where the item is stored in S3.