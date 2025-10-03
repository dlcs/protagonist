# ElasticTranscoder replacement

* Status: decided
* Deciders: Donald Gray, Tom Crane
* Date: 2025-18-18

## Context and Problem Statement

DLCS has always used AWS [ElasticTranscoder](https://aws.amazon.com/elastictranscoder/) for media transcoding. 

As of 13th November 2025, AWS are discontinuing this service so we need to use an alternative means of transcoding media files.

## Decision Drivers

* Feature compatible - The new service should support all existing use-cases we have for media transcoding.
* Ease of change - There should be minimal code involved to use new service.
* Ease of management - Ideally scaling, configuring, updating etc should be relatively straightforward.

## Considered Options

* Use AWS [Elemental MediaConvert](https://aws.amazon.com/mediaconvert/)
* Use 'ffmpeg-as-a-service'

## Decision Outcome

Chosen option is: _"Use AWS [Elemental MediaConvert](https://aws.amazon.com/mediaconvert/)"_

MediaConvert is the recommended ElasticTranscoder replacement service within AWS. There are blog articles and helper scripts to aid with migration, e.g. https://aws.amazon.com/blogs/media/migrating-workflows-from-amazon-elastic-transcoder-to-aws-elemental-mediaconvert/

### Positive Consequences

* MediaConvert should be as "like for like" as can be as it is the recommended replacement.
* Similar without being identical. For example, uses CloudWatch events rather than SNS to raise notifications - different route to same outcome.
* Scope to add other enhancements in future, e.g.
  * ET jobs can read from 1 single bucket but MC can read from many. This could include Customer buckets similar to how we read JP2s for optimised origins.
  * ET had 'presets' which dictate how a job is to be run. MC has 'templates', which are similar but can be altered for 1-off jobs.

### Negative Consequences

* Pricing model is slightly more complicated.
* May involve some additional work to replicate everything we do with ET, specifically thinking of API calls that return known presets.

## Pros and Cons of the Options

### Use 'ffmpeg-as-a-service'

#### Positive Consequences

* We would have greater control on how videos are transcoded.
* This is likely what services like ET and MediaConvert are doing under the hood.
* Other services, like Avalon, successfully use this approach.

#### Negative Consequences

* Complexity - identifying exactly how to transcode all formats can be difficult.
* We would likely end up replicating a lot of what a managed service does (put asset in bucket, ping API, receive 'done' notification).