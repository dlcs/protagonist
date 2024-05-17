# Asset modified cleanup

## Brief

As part of the delivery channels work, it's possible to modify an asset to either change a delivery channel, or update said delivery channel and leave orphaned assets in S3.  This RFC is to discuss how to remove these assets safely and the various permutations of this removal logic.

## API

Currently, when an asset is modified, a message is published to an SNS topic by the `AssetNotificationSender` with the `before` and `after` of the asset.  This process can then fan out to SQS queue which is then handled by a service that cleans up modified assets in a similar process to how `DELETE` requests are handled.

### Changes

As part of this process there will need to be some changes made to the API, which are as follows:

- AssetModifiedMessage needs to be raised whenever an asset is changed. It's currently only happening for single image requests - PUT, POST or PATCH `/customers/{c}/spaces/{s}/images/{i}` as well as `/reingest` too.
  - Needs to happen for bulk operations (batch PATCH + queue).
  - There's already logic to handle batch sending of notifications, just need to get list of messages to send.
- It's possible to send `AssetModified` messages that aren't required to be ingested (such as metadata changes) and this is primarily controlled by `processAssetResult.RequiresEngineNotification`. In order to reduce churn, an attribute should be added to the request that indicates the asset will be ingested by engine
  - This attribute should be called something like `EngineNotified`
- Asset requires the `DeliveryChannelPolicyId` to work out differences in policies (this should be there already)
- Asset requires `roles` as changes to roles can mean the `info.json` needs to be removed
- API should not be responsible for deciding when cleanup is conducted

## AWS

As part of the changes, there will need to be some changes to the AWS estate to support the changes to asset modified.  Primarily, this will be updating the current SNS topic and adding an SQS listener to listen for asset modified notifications.  The queue should forward all requests to the listener as there's a possibility that some changes are not captured purely by engine ingest, such as role changes

### Specifics

- `delay_seconds` will be used to delay messages being added to the queue, to avoid issues with multiple ingests
- a dead letter queue should be added, as well as `visiblityTimeout` and `maxRetries`, this will help to avoid issues where assets are continually reprocessed

## Handler

After being added to the SQS queue, it needs to be handled by the cleanup handler being extended.  The reason to use a .Net application, is that there will be a lot of shared logic with Engine (for example, working out storage keys) and that access methods for the database and S3 have been written for the DLCS.  Additionally, a new implementation of `IMessageHandler` will be created that's designed to handle `AssetModified`

### Specifics

- Assets should be checked for ingestion complete from the `Finished` property before being processed, as well as the `Error` property and `Ingesting`.
  - The `Finished` property will be checked as current > `Before` to decide if work needs to be done
  - The `Ingesting` property shows if the asset is currently being ingested.  In this case, the message needs to be requeued
  - This has some implications on assets which do not get completed in the database being continually reprocessed.  As such, a dead letter queue should be implemented after a certain number of retries using the `maxRetries` variable.  Additionally, a sensible retention period needs to be decided on the DLQ
  - Ingestion completion is needed so that we can check when the asset was last ingested, and whether the attached policy has changed in that time
  - If there's an error, no need to do anything
- Telling the difference between `Before` and `After` should be done with using the `Before` from the message, but the `After` be pulled from the database, to avoid issues with multiple reingests happening
  - This would help to avoid issues where the asset is changed, then changed back afterwards, but due to `delay_seconds`, the `After` on the message clears up the correct derivatives

### Logic

#### Thumbs recalculation

Within this, the most complex part of this is recalculating thumbnails.  The general approach to this will be to use the policy of the current asset, along with the thumbnails that currently exist within S3.  This will require a `ListBucket` operation from S3 which has a cost implication.  This could mean that checking 53 million images, would incur a cost of approximately $265

Due to the cost, calls to `ListBucket` should be limited

Thumbs should use the iiif-net library to check expected sizes of thumbnails for an image, and there needs to be some logic to not remove thumbs that are within 2-3 pixels of the expected to avoid off-by-one errors in thumbnail generation.  These off-by-one errors occur due to rounding errors in cantaloupe thumbs generation, more details can be found [here](https://github.com/dlcs/protagonist/pull/819)

System thumbs will need to be left alone.  In order to make it so this doesn't differ from the system thumbs in engine, a  value needs to be created that can be used by both engine and the cleanup handler.  This is used by these projects through the options pattern in .Net. One of the ways to do this, is a parameter store variable that is exposed to the engine and orchestrator via an environment variable

Finally, maxUnauth/Roles will need to be taken into account as storage in the bucket will have a prefix of `/open` or `/auth` based on this.

#### Named query derivatives

There are many different permutations of objects in named queries, so for now this is out of scope for cleanup.

#### Update types

There are a number of ways that an update to an asset can cause changes to stored derivatives that aren't tracked.  It can roughly be divided into 4 categories of change:

- Delivery channel changed
- Policy id changed
- Roles changed
- Policy data updated
- Origin changes

#### Delivery channels changed

This becomes an issue when a delivery channel is removed from an asset, with the following implications:

- iiif-img removed
  - Stored iiif-img derivative needs to be removed
  - `info.json` removed
  - Asset can exist at both filename and `/original` path - though the `original` needs to be left if the file channel exists on the asset
  - if `thumbs` not there, then `s.json`, asset application metadata and thumbnails need removed as well 
- Thumbs removed
  - `info.json` removed
  - If removing "thumbs" leaves "iiif-img":
    - Do not touch `s.json`
    - Do not touch `AssetApplicationMetadata`
    - Leave system thumbs
  - If removing "thumbs" doesn't leave "iiif-img":
    - Delete `s.json`
    - Delete `AssetApplicationMetadata`
    - Delete all thumbs
- iiif-av removed
  - Timebased derivative removed
  - Metadata removed
- File removed
  - The asset at origin should be removed if there's an asset on the `/original` path - should only be removed if `iiif-img` is not using it
- None removed
  - Nothing required

#### Policy id changed

This is that the delivery channel stays the same, but the id of the policy has changed.  It should be able to be detected by checking the delivery channel policy id in `before` against the current asset, with the following implications:

- iiif-img changed
  - `info.json` needs removed
  - If it moves to a `use-original` policy, the derivative asset can be removed
  - If it moves away from `use-original`, then the `/original` asset can also be removed, provided there isn't a `file` channel
- Thumbs changed
  - Thumbs need to be removed that are no longer required.
  - `s.json` and asset application metadata should be updated - `s.json` should be updated by the reingest
  - `info.json` removed
- iiif-av changed
  - Old transcode derivative removed if the file extension is no longer required
- File changed
  - The asset at origin should be removed if there's an asset on the `/original` path - should only be removed if `iiif-img` is not using it

#### Roles changed

This should only have an implication on the `info.json`, which would need to be removed

#### Policy data updated

The policy data being updated can be found from the date that the delivery channel policy was updated, after the `finished` date of the current asset itself. 

- iiif-img changed
  - `info.json` needs removed
  - If it moves to a `use-original` policy, the derivative asset can be removed
  - If it moves away from `use-original`, then the `/original` asset can also be removed, provided there isn't a `file` channel
- Thumbs changed
  - Thumbs need to be removed that are no longer required.
  - `s.json` and asset application metadata should be updated - `s.json` should be updated by the reingest
  - `info.json` removed
- iiif-av changed
  - Old transcode derivative removed if the file extension is no longer required
- File changed
  - The asset at origin should be removed if there's an asset on the `/original` path - should only be removed if `iiif-img` is not using it

  ## General comments

  In the case of large reingests, the image could be processed by the handler before the image is updated by engine due to the longest `delay_seconds` being 15 minutes. In this case, the best option would be to disable the handler until after the reingest is completed.