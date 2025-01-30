# Querying Across IIIF Presentation and DLCS

[IIIF-Presentation](https://github.com/dlcs/iiif-presentation) (IIIF-P) is a companion service for Protagonist. Protagonist is Asset-Delivery centric, it doesn't primarily concern itself with IIIF. IIIF-P is IIIF-centric, its unit of work are IIIF Manifest and Collections. It directly interfaces with DLCS-API to make it easy to create a Manifest and ingest assets at the same time.

## Separate Concerns?

Early on we decided to treat Asset-Delivery and IIIF-P as 2 separate concerns contexts ("bounded contexts" if we were using DDD). They would be entirely separate - different codebases, database, release cadence etc. They could, however, arguably be considered as one single context.

IIIF-P is effectively a client of DLCS-API, it doesn't have 'special' internal access, all integrations are done via the public API. However, this comes at a cost - what could be a simple DB join between `canvasPaintings` and `assets` becomes HTTP request(s) which is orders of magnitude slower and more complex.

The touchpoints between the 2 services are:
1. _Do these 1:n assets exist?_ - e.g. if IIIF-P receives a manifest containing assets we need to know if they exist or not:
   * Over HTTP this is a bulk request(s) to `POST customer/{id}/allImages` specifying lots of separate ids. Implementation wise in DLCS this isn't particularly efficient to query as it's effectively running `select * from images where id in (@list_of_ids);`. Also difficult to cache as the key would be a list of ids.
   * If we had a shared context this would effectively be the same, without the overhead of HTTP request.
2. _Get details of all assets in manifest_ - e.g. IIIF-P responding to an authenticated API request for a manifest
   * Over HTTP this is as above.
   * With shared context this would be much more efficient as we could join to the `canvas_painting` table: `select * from canvas_paintings cp left join images i on cp.asset_id = i.asset_id where cp.manifest_id = @manifest_id`
3. _Ingest Assets_ - e.g. IIIF-P receives a POST or PUT request for manifest containing new assets
   * Over HTTP IIIF-P directly passes the `"assets"` to the DLCS API to validate and ingest.
   * If there was a shared context I would envisage this working the same to avoid having multiple paths to ingest assets.
4. _Get IIIF content-resources for all assets in manifest_ - e.g. IIIF-P is building manifest using assets in DLCS
   * Over HTTP this can be efficiently done via NamedQueries. This is currently working by using `batch=p1` NQ parameter, where we can specify multiple batch-ids. However, querying for batches in this manner isn't effective for manifest updates as the manifest payload may contain assets that exist in DLCS already so, no need to create a batch = no known batch-id to query on (outlined below)
   * With shared context this would be same query as (2) and we could share the `IIIFCanvasPainting` logic in Orchestrator that builds out manifest.

## Pros / Cons

Given the above, there are definitely arguments for sharing _stuff_ between the 2 - exactly what that sharing entails could differ, variations of:

* Database
  * Sharing same DB, with single EF context
  * Sharing same DB but different schemas (allows cross-schema querying)
  * As above, different schemas
* Code
  * Everything in same solution, sharing class library code
  * Separate solutions, sharing some code. Via nuget, git submodules or other

Below is a list of the pros + cons for having the 2 services separate:

### Pros

* Interactions via API, avoids lockstep deployments (assuming no breaking changes!)
* While some logic could be shared via code, some is definitely better confined within relevant services. E.g.
  * Mapping methods like `Image ToHydra(this Asset dbAsset)` should only belong in the API, rather than shared lib. To expose `"assets"` as they appear in DLCS we would need to use this.
  * `IIIFCanvasFactory` could be shared but if this changes in the future to be aware of the image-server being used (e.g. image-server only supports Image2) this logic would need to be shared by Orchestrator, which it's relevant to, and IIIF-P, which it isn't relevant to.
* Protagonist is a mature codebase, the more that is shared and moved the bigger risk of running into issues
* Existing deployments that have no need of IIIF-P can stay on DLCS (asset-delivery) only, no need that a table or code that'll never be used is introduced.
* Conversely, IIIF-P could be used as a 'pure' IIIF store without ever ingesting assets (unlikely to happen but possible).
* Removes risk that the asset-delivery and presentation concerns accidentally bleed into one another (the below suggestion _does_ bleed presentation concerns into asset-delivery but it is considered and not accidental).

### Cons

* Interactions between the 2 can be cumbersome and bloated, what could be a simple DB call becomes a large HTTP request
* Possible degradation in performance if API overloaded
* Simplification of overall solution

## Suggested Solution

The main "con" in separating the concerns is the ease of querying for assets across services. As noted above this isn't very efficient.

To alleviate this we could introduce a new property to asset - `"manifests"`. This would store an array of 0:n manifest identifiers that make bulk querying easier.

> [!Note]
> This is deliberately `"manifests"` to make it obvious what the use case is. An alternative like `"app_metadata"` seemed too vague (but could be useful in the future).

The main point here is that we _must_ ensure that the DLCS Assets are kept in sync with which manifests they are in. Any addition or deletion actions must be succesfully reflected in DLCS.

This doesn't solve all problems; there is still the `"Do these assets exist?"` request to `/allImages` that needs to be made. However, it will reduce the number of bulk requests we need to make to DLCS.

### Querying for Assets

The [Asset Query](https://deploy-preview-3--dlcs-docs.netlify.app/api-doc/asset-queries) syntax would be extended to allow for querying on `"manifests"`, similar to `"tags"` in the linked documentation. This would allow us to query `/allImages` without POSTing lists of ids and the resulting SQL will be more efficient:

```sql
-- POST /allImages {"members": [ {"id": "2/1/foo"}, {"id": "2/1/bar"}]}
select * from images where id in ('2/1/foo', '2/1/bar');

-- POST /allImages {"members": [ {"id": "2/1/foo"}, {"id": "2/1/bar"},...{"id": "2/99/qux"}]}
select * from images where id in ('2/1/foo', '2/1/bar',...'2/99/qux');

-- GET /allImages?q={"manifest": ["m123"]}
select * from images where manifest ? 'm123';

-- GET /allImages?q={"manifest": ["m123", "m125"]}
select * from images where manifest ?| array['m123', 'm125'];

-- GET /allImages?q={"manifest": ["m123", "m125"]}?orderBy=id
select * from images where manifest ?| array['m123', 'm125'] order by id;
```

### Querying for Content-Resources

A new NamedQuery property can be introduced, which would allow us to query get content-resources for all images in a manifest. `GET /iiif-resource/for-manifest/m123`.

There is already an unused [`manifest`](https://github.com/dlcs/protagonist/issues/825) property for NamedQueries. This is the ideal name for the property but, as mentioned in the ticket, we need to determine if Deliverator used this as a number of NamedQueries in the database are configured with this property.

### Side Effects

There will be some side effects as a result of implementing the above:

* Not all manifest will have a related batch in the IIIF-P database. 
  * Will this change anything?
  * State of "Manifest ready to process" cannot solely be determined by batch completion status.
* If manifest ingestion is not required, some Manifests can synchronously be returned at API PUT or POST time
  * Do we want to do this? Or continue with async approach?
  * If so, do we need an alternative entry point to manifest generation that is not manifest dependant?

### Example of calls made

To outline how this suggestion would affect interactions, the below is a sample timeline of adding and updating a manifest and interactions between the 2 services

#### 00:00 IIIF-P. manifest "m123" is created with new assets `"2/1/foo"` and `"2/1/bar"`
| Driver                         | Without "manifest"                                                                                             | With "manifests"                                                                                              |
| ------------------------------ | -------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| Check if assets new            | Query `canvasPainting` to check if asset exists in iiif-p, fallback to `POST customers/{id}/allImages` request | As Without                                                                                                    |
| Assets are new                 | `POST customers/{id}/queue` with 2 assets (batch 55555 created)                                                | Assets are new, POST `customers/{id}/queue` with 2 assets, including `"manifest"` field (batch 55555 created) |
| Get `"assets"` for return body | `POST customers/{id}/allImages` with 2 Ids                                                                     | `GET customers/{id}/allImages?q={"manifest": ["m123"]}`                                                       |
| DB Results                     | Manifest "m123". Batch 55555. CanvasPainting [`"2/1/foo"`, `"2/1/bar"`]                                        | As Without                                                                                                    |


#### 00:01 DLCS. Ingests and raises batchCompletion notification

#### 00:02 IIIF-P picks up batchCompletion notification
| Driver                | Without "manifest"                       | With "manifests"                           |
| --------------------- | ---------------------------------------- | ------------------------------------------ |
| Get content-resources | `GET /iiif-resource/2/batch-query/55555` | `GET /iiif-resource/2/manifest-query/m123` |

#### 00:03 Auth'd API request to GET manifest
| Driver                         | Without "manifest"                         | With "manifests"                                        |
| ------------------------------ | ------------------------------------------ | ------------------------------------------------------- |
| Get `"assets"` for return body | `POST customers/{id}/allImages` with 2 Ids | `GET customers/{id}/allImages?q={"manifest": ["m123"]}` |

#### 01:00 IIIF-P. manifest "m123" updated with existing assets `"2/1/foo"`, `"2/1/bar"`, `"2/1/baz"` and new asset `"2/1/qux"`
| Driver                         | Without "manifest"                                                                                                                                   | With "manifests"                                                                                                                                                               |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Check if assets new            | Query `canvasPainting` to check if asset exists in iiif-p (2 do), fallback to `POST customers/{id}/allImages` request for rest (1 exists, 1 doesn't) | As Without                                                                                                                                                                     |
| 1 asset new                    | `POST customers/{id}/queue` with 1 asset (batch 66666 created)                                                                                       | 1 assets is new, `POST customers/{id}/queue` with 1 asset (batch 66666 created). 3 assets exist, `PATCH customers/{id}/space/{id}/images` to add "m123" to "manifest" property |
| Get `"assets"` for return body | `POST customers/{id}/allImages` with 4 Ids                                                                                                           | `GET customers/{id}/allImages?q={"manifest": ["m123"]}`                                                                                                                        |
| DB Results                     | Manifest "m123". Batch [55555, 66666]. CanvasPainting [`"2/1/foo"`, `"2/1/bar"`, `"2/1/baz"`, `"2/1/qux"`]                                           | As Without                                                                                                                                                                     |

#### 01:01 DLCS. Ingests and raises batchCompletion notification

#### 01:02 IIIF-P picks up batchCompletion notification
| Driver                | Without "manifest"                            | With "manifests"                           |
| --------------------- | --------------------------------------------- | ------------------------------------------ |
| Get content-resources | `GET /iiif-resource/2/batch-query/55555,6666` | `GET /iiif-resource/2/manifest-query/m123` |

> [!Important]
> 55555 + 66666 are the 2 batches we created and only contain 3 of 4 assets we know about. How do we get content-resources for `"2/1/baz"`?
> One option would be to use `/allImages`, get the distinct batchId's from that and use those in NQ response (e.g. `/iiif-resource/2/batch-query/55555,66666,123123`) but 123123 could contain 100 images and we only want 1. Or we may need to know content-resources for 10 images, each with a different batchId and each batch has 100 images....

#### 01:03 Auth'd API request to GET manifest
| Driver                         | Without "manifest"                         | With "manifests"                                        |
| ------------------------------ | ------------------------------------------ | ------------------------------------------------------- |
| Get `"assets"` for return body | `POST customers/{id}/allImages` with 4 Ids | `GET customers/{id}/allImages?q={"manifest": ["m123"]}` |

## Disregarded Approaches

The above resulted from exploring how we will manage Manifest updates. One suggestion was to always follow the same process of IIIF-P creating Batches and allowing the DLCS to determine whether things need processed or not. This was disregarded as we would need some means of notifying `POST customers/{id}/queue` to only ingest if something has changed.

One consideration was allowing the IIIF-P readonly access to the DLCS DB for querying if assets exist. This would be quicker but ties the IIIF-P implementation to the DB internals of DLCS.

## Questions

* How to do batch patch addition to `"manifests"`, without needing to define them all?
  * [JSON Patch](https://datatracker.ietf.org/doc/html/rfc6902#section-4.1) has a syntax for this : `{ "op": "add", "path": "manifests", "value": [ "m123" ] }`
* Do we want a batch patch at customer level? It's currently at space level, which I think is fine but could result in multiple calls.
* Is `"manifests"` the correct name? Or is `"appMetadata"` with a key enough?
* Above SQL examples assume jsonb, should we consider `text[]` or separate table?