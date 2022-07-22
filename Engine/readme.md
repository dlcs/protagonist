# Engine

Engine is responsible for ingesting assets; either synchronously via an API call or by monitoring a queue.

## Overview

### API

The engine has 2 routes for synchronous processing:

* `/asset-ingest` - Process incoming `IngestAssetRequest` - generating derivatives for asset delivery.
* `/image-ingest` - As above but takes `LegacyIngestEvent`, which is Deliverator notification model. The `LegacyIngestEvent` is converted to `IngestAssetRequest` and follows exact same process as above.

### Queue

The engine also starts has a `BackgroundService`, `SqsListenerService` for asynchronous processing. 

This will subscribe to any queues that are configured in appSettings. The possible queues are:

* ImageQueue - Images queued for processing ("I" family). Process is as `/asset-ingest` with an alternative entrypoint. Can handle either `IngestAssetRequest` or `LegacyIngestEvent` messages.
* PriorityImageQueue - Exact same process as above, "priority" queue is used less often so will have less items queued.
* TimebasedQueue - Timebased assets queued for processing ("T" family). Not yet implemented.
* TranscodeCompleteQueue - Listens for notifcations from ElasticTranscoder and finishes processing of Timebased asset. Not yet implemented.