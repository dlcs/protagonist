# Asset modified cleanup

## Brief

As part of the delivery channels work, it's possible to modify an asset to either change a delivery channel, or update said delivery channel and leave orphaned assets in S3.  This RFC is to discuss how to remove these assets safely and the various permutations of this removal logic.

## API

Currently, when an asset is modified, a message is published to an SNS topic by the `AssetNotificationSender` with the `before` and `after` of the asset.  This process can then fan out to SQS queue which is then handled by a service that cleans up modified assets in a similar process to how `DELETE` requests are handled.

### Changes

As part of this process there will need to be some changes made to the API, which are as follows:

- AssetModifiedMessage needs to be raised whenever an asset is changed. It's currently only happening for single image requests - PUT, POST or PATCH `/customers/{c}/spaces/{s}/images/{i}` (maybe `/reingest` too).
  - Needs to happen for bulk operations (batch PATCH + queue).
  - There's already logic to handle batch sending of notifications, just need to get list of messages to send.
- It's possible to send `AssetModified` messages that aren't required to be ingested (such as metadata changes). In order to reduce churn, an attribute should be added to the request that indicates the asset will be ingested by engine
  - This attribute should be called something like `EngineNotified`
- asset requires the `DeliveryChannelPolicyId` to work out differences in policies (this should be there already)
- asset requires `roles` as changes to roles can mean the `info.json` needs to be regenerated

## AWS

As part of the changes, there will need to be some changes to the AWS estate to support the changes to asset modified.  Primarily, this will be updating the current SNS topic and adding an SQS listener to listen for asset modified notifications.  Additionally, the queue needs to be set to only listen for assets that have a `EngineNotified` attribute. 

### Questions

- API will raise this notification at the same time as either sync or async calling engine - if the latter and there's high traffic then the 'cleanup' could happen before the engine has done its work. Is this an issue? Do we want a high `delay_seconds` in SQS? Means it'll be cleaned up eventually, not necessarily now. 

## Handler

After being added to the SQS queue, it needs to be handled by either the cleanup handler being extended, or a new .Net application.  The reason to use a .Net application, is that there will be a lot of shared logic with Engine (for example, working out storage keys) and that access methods for the database and S3 have been written for the DLCS.

### Specifics

- Assets should be checked for ingestion complete from the `Finished` property before being processed
  - This has some implications on assets which do not get completed in the database being continually reprocessed.  As such, a dead letter queue should be implemented after a certain number of retries using the `maxRedrives` variable.  Additionally, a sensible retention period needs to be decided on the DLQ
  - ingestion completion is needed so that we can check when the asset was last ingested, and whether the attached policy has changed in that time
- telling the difference between `before` and `after` should be done with using the `before` from the message, but should the `after` be pulled from the database?
  - This would help to avoid issues where the asset is changed, then changed back afterwards, but due to `delay_seconds`, the `after` on the message clears up the correct derivatives

### Logic

#### Thumbs recalculation

Within this, the most complex part of this is recalculating thumbnails.  In general approach to this will be to use the policy of the current asset, along with the thumbnails that currently exist within S3.  This will require a `ListBucket` operation from S3 which has a cost implication.  This could mean that checking 53 million images, would incur a cost of approximately $265

Due to the cost, calls to `ListBucket` should be limited

#### Named query derivatives

Part of the key is the name of the named query - how to handle this?  Possibly have a list of key's to check/delete against?


#### Update types

There are a number of ways that an update to an asset can cause changes to stored derivatives that aren't tracked.  It can roughly be divided into 4 categories of change:

- delivery channel changed
- policy id changed
- roles changed
- policy data updated

#### Delivery channels changed

This becomes an issue when a delivery channel is changed away from a specific policy, with the following implications:

- iiif-img removed
  - stored iif-img derivative needs to be removed
  - `info.json` needs regenerated
  - NQ derivatives (like pdf/zip) need to be removed
- thumbs removed
  - thumbs derivatives need to be removed
  - `info.json` needs regenerated
  - asset metadata for thumbs removed in database
- iiif-av removed
  - timebased derivative removed
  - `info.json` needs regenerated
  - metadata removed
  - timebased input removed?
- file removed
  - the asset should still be used by another channel, so no need to change anything
- none removed
  - nothing required

#### Policy id changed

This is that the delivery channel stays the same, but the id of the policy has changed.  It should be able to be detected by checking the delivery channel policy id in `before` against the current asset, with the following implications:

- iiif-img changed
  - `info.json` needs regenerated
  - NQ derivatives need to be regenerated
  - if it moves to a `use-original` policy, is there a need to remove the asset as well?
- thumbs changed
  - thumbs need to be removed that are no longer required.
  - `info.json` needs regenerated
  - s.json and asset application metadata should be updated
- iiif-av changed
  - old transcode derivative if the file extension changes?
- file changed
  - no changes needed?

#### Roles changed

This should only have an implication on the `info.json`, which would need to be regenerated

#### Policy data updated

The policy data being updated can be found from the date that the delivery channel policy was updated after the `finished` date of the before asset itself. 

- iiif-img changed
  - `info.json` needs regenerated
  - NQ derivatives need to be regenerated
  - if it moves to a `use-original` policy, is there a need to remove the asset as well?
- thumbs changed
  - thumbs need to be removed that are no longer required.
  - `info.json` needs regenerated
  - s.json and asset application metadata should be updated
- iiif-av changed
  - old transcode derivative if the file extension changes?
- file changed
  - no changes needed?