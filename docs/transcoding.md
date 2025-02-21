# Transcoding

The Engine uses AWS ElasticTranscoder to transcode source files to web-friendly derivatives.

This document outlines some specific properties we use that are specific to DLCS.

## User Metadata

We make use of the optional [User Metadata](https://docs.aws.amazon.com/elastictranscoder/latest/developerguide/job-settings.html#job-settings-user-metadata) property when creating a job.

This is an optional list of up to 10 key/value pairs that can be passed to a job. These are then included in the return payload. The values we add are (these are all in the `UserMetadataKeys` class):

* `dlcsId` - this is the Id of the DLCS asset (e.g. `10/99/foo`) and used to identify which asset this job is for.
* `startTime` - the ticks when job was created.
* `jobId` - a random GUID associated with job. This is used as a prefix path for job outputs. Allows the same Asset to have multiple 'in-flight' transcodes operation without latter jobs overwriting previous. _See below for further details_.
* `storedOriginSize` - this key stores the size, in bytes, of any additional bytes that have been stored as part of ingest operation. As AV ingest is asynchronous, this value is added to the total size of transcoded files to find the current `ImageStorage.Size` value. If no additional bytes stored this will be 0.

## Metadata API endpoint

The API exposes `customers/{customerId}/spaces/{spaceId}/images/{asset}/metadata` endpoint that returns ET job information for AV items. This works by:

* On creation of ET job, the AWS ElasticTranscoderJobId is stored at a known key in S3 (in an XML element, mimicing deliverator).
* When GET request received to above endpoint, this key is read then a `ElasticTranscoder:ReadJob` request is made and results are returned, after parsing.

### Note on JobId

The output from above API endpoint normalises the output keys to make easier to read. The `TranscoderJob` class has some logic to work out the correct path as deliverator and protagonist use different methods. See private method `TranscoderOutput.GetOutputKey()` for details