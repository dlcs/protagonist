# Revisit Batches

See [#491](https://github.com/dlcs/protagonist/issues/491) for original ticket outlining this issue.

Currently a "Batch" is a loose and transient grouping of images. An image can belong to 1 batch at any given time, no history is maintained. This worked well for the initial use-case of bulk ingestion of images and tracking process for things like progress bars. However for more complex scenarios this connection is lacking and improving it 

This RFC outlines proposal for improving batches, this will allow us to:
* Get a list of assets in a batch, not just the current batch.
* Extend named query support to filter on batchId(s)
* Avoid issues with double counting completed or failed assets
* Track which batches an images has been ingested in
* Identify which assets failed within a batch
* Reingest any failed assets in a batch (see [#402](https://github.com/dlcs/protagonist/issues/402))

Note that not all of the above points are addressed here, some will need new API endpoints to be added building on the change outlines here.

## Proposal

The proposal is to introduce a new table, `BatchAssets`. This will track the assetIds per batch, maintaining the history of batch:asset. 

The following is the proposed schema:

| Column   | Type        | Description                                    |
| -------- | ----------- | ---------------------------------------------- |
| BatchId  | int         | Batch identifier                               |
| AssetId  | text        | Asset identifier                               |
| Status   | int         | Values of waiting/completed/error map to enum. |
| Finished | timestamptz | Date when asset finished                       |

> Note: 
> * `Status` represents the status of processing asset in this batch, not _overall_ status of asset - it could have failed in batch 12345 but then been successful in batch 12999.
> * `Finished` will store when the asset finished processing, we don't need to store `Created` as this will be stored against the batch.

The `Batch` table will maintain it's current schema, this will serve as a quick status check for a batch.

### Changes Required

### API Endpoints

The `/customers/{customerId}/queue/batches/{batchId}/images` endpoint will stay as-is and return all images currently associated with a batch (ie `select * from images where batch = @batch_id`). This will allow any existing integrations to continue working without changes. All other batch endpoints will stay as-is - we may update in the future but not now.

A new endpoint, `/customers/{customerId}/queue/batches/{batchId}/assets`, will be introduced. This is effectively `v2` of the above endpoint but allows us to introduce the more appropriate `/assets` concept. This will use the new `BatchAssets` table and will allow us to return images from any batch, not just the current one, (ie `select i.* from images i inner join batch_assets ba on i.id = ba.asset_id where ba.batch_id = @batch_id`).

### API Batch Processing

When a new Batch is created via the API, 1:n `BatchAsset` records will be created. Initially these will just have AssetId, BatchId and "Waiting" status.

On asset completion, the appropriate `BatchAsset` record will be updated with relevant status and Finished date.

The counts on `Batches` table will be updated to reflect the overall counts for batch but rather than incrementing the counters the overall values should be derived from those in `BatchAsset`. This should avoid incorrect counts issue, see [#852](https://github.com/dlcs/protagonist/issues/852).

### Orchestrator

Extend the named query syntax to support `?batch` value to be specified, supporting multiple comma delimited values. `?batch=1234,5555,9999`.

## Historical Data Considerations

There are a number of mature Protagonist deployments that use Batches heavily. I don't think we need to do any backporting of data into `BatchAssets` as all we would get is a snapshot of the current data.

## Future Operations

Depending on API usage patterns, the `BatchAssets` table will likely contain a large number of rows that could grow over time. We may need to look at a periodic cleanup of assets from this table in the future. 

The exact process for doing this will need to be determined - consider removing only rows for Assets that completed without error, or those that are older than XX days, or have been ingested in a subsequent batch....

## Questions

* Do we want to have a single `Status` field, or separate boolean status fields? I opted for `Status` as each `BatchAsset` can only be in 1 single state at any given time.