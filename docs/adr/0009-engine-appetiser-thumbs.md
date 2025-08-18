# Revert to Appetiser for Thumbs

* Status: decided
* Deciders: Donald Gray, Tom Crane
* Date: 2025-08-18

## Context and Problem Statement

Engine was updated to use Cantaloupe image server for thumbs in [v1.3.0](https://github.com/dlcs/protagonist/releases/tag/v1.3.0), see [ADR 0006](0006-engine-imageserver.md) for details.

While ideal in theory, in practice this change has made the overall ingest process unreliable. The failure rate depends on the number of ingest requests being made, with large batches of ingest requests for images that are already JPEG2000s faring worst. Working theory is that there is less work to do as we don't need to convert so higher throughput of thumbs requests result in more frequent failures.

We need to stabilise the thumbnail generation process while maintaining use of [IIIF ImageAPI size parameters](https://iiif.io/api/image/3.0/#42-size) in thumbnail policies.

We will continue to use Cantaloupe as image-server for serving image traffic. The issue we are ecountering with thumbs generation seems related to the use of S3 as source.

## Decision Drivers

* Stability - The ingest process was robust and rarely encountered errors prior to switch to Cantaloupe.
* Performance - Thumbnail generation has dramatically slowed down the ingest process.
* Compute reduction (and therefore cost reduction) - In an effort to alleviate the above issues we have had to aggressively scale the "EngineThumbs" service to meet demand.

## Considered Options

* Run Cantaloupe via an alternative means (ie not streaming from S3)
* Use an alternative image-server, e.g. [IIPImage](https://github.com/ruven/iipsrv), [iiif-processor](https://www.npmjs.com/package/iiif-processor) or [laya](https://github.com/digirati-co-uk/laya/)
* Update [Appetiser](https://github.com/dlcs/appetiser) image and revert to previous method.

## Decision Outcome

Chosen option is: _"Update [Appetiser](https://github.com/dlcs/appetiser) image and revert to previous method."_

Appetiser, and its predecessor Tizer, were successfully used for image conversion and thumbnail generation from initial DLCS release until v1.3.0 was release in May '24. 

The Appetiser Docker image will be updated to handle non-confined IIIF ImageApi size parameters.

### Positive Consequences

* The ingest process had been stable and reliable so the best option for now is to revert back to using Appetiser.
* Meets all the criteria for decision drivers.

### Negative Consequences

* There is now a difference between _thing that pre-generates thumbnails_ and _thing that serves image-requests_ meaning there may be very slight differences in how these are rendered.
* There are some open issues related to previous Appetiser use that may need to be addressed (e.g. [#840](https://github.com/dlcs/protagonist/issues/840) and [#684](https://github.com/dlcs/protagonist/issues/684))

## Pros and Cons of the Options

### Run Cantaloupe via an alternative means (ie not streaming from S3)

#### Positive Consequences

* Service pre-generating thumbnails and serving image-requests is consistent.

#### Negative Consequences

* Additional service to manage.
* Potential risk of similar issues arising.

### Use an alternative image-server

#### Positive Consequences

* Service pre-generating thumbnails and serving image-requests is consistent.

#### Negative Consequences

* Unfamiliarity with iiif-processor, would need to invest time to profile to avoid issues similar to those encountered with Cantaloupe.
* Laya is incomplete.
* IIPImage is known to be very performant but would require more complex infrastructure as it cannot read from S3.