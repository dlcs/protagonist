# Stub Assets and Adjuncts

This RFC looks at how we can enable "stub" assets to store adjuncts and how we can carry out bulk adjunct operations for multiple assets. 

Stub assets are assets that won't ever have any binary content themselves, they exist purely to store adjuncts.

The requirement came from the need for IIIF-Presentation to store adjuncts for arbitrary IIIF resources but it could be useful in other instances where we want adjuncts without storing any binary asset content, while still taking advantage of access control, customer origin strategies etc.

## Assets Without Content

The simplest way to have assets without content is to create them with the [none delivery-channel](https://dlcs.github.io/public-docs/api-doc/delivery-channels/#the-none-channel).

This signifies that we don't want to deliver the asset. When the engine receives a notification for an asset with `none` delivery-channel it shortcuts processing and marks the asset as ingested.

We could leverage the `none` channel but what about `"origin"` and `"mediaType"`? Alongside `"id"` these are the minimal fields required to register an asset.

```json
{
    "id": "example",
    "origin": "s3://bucket/image.jpg",
    "mediaType": "image/jpeg"
}
```

We could require that the consumer pass some known placeholder values, similar to the "unobtainable" role from [ADR-0010](https://github.com/dlcs/protagonist/blob/develop/docs/adr/0010-replace-maxunauthorised.md), or they are free to use these columns for their own means - they are effectively ignored.

```json
{
    "id": "example",
    "origin": "null-origin",
    "mediaType": "binary/none"
}
```

or, using example from IIIF-Presentation RFC-0006, for a range:

```json
{
    "id": "rng_iiif.io_api_cookbook_recipe_0024-book-4-toc_range_r0",
    "origin": "https://iiif.io/api/cookbook/recipe/0024-book-4-toc/range/r0",
    "mediaType": "application/ld+json"
}
```

The above, while functional, doesn't seem quite right. It would be better to have an alternative means of registering undeliverable assets. The suggested approach to this is to use a special space for all stub assets.

## Special Space (0)

When creating spaces, customers will get the next available int identifier, or they can specify their own. The special space needs a set identifier that can't be used - we could pick an arbitrary number, say 10 or 100, but this could be used by an existing customer. The alternative is to use a number that won't already be in use, the suggested space is `0` - this won't be used elsewhere and reads better in URLs than a negative space or something like `int.MaxValue`.

There is an outstanding bug highlighting that it is possible to create spaces <= 0, this should be addressed as part of implementing [#997](https://github.com/dlcs/protagonist/issues/997).

> [!WARNING]
> Ideally we would be able to restrict the access of space 0 to specific callers or operations only. 
>
> If we supported JWT tokens and claims we could specify what consumers can use this space.

### Specific Logic

If we use space 0 for _all_ stub assets we can make the validation logic work slightly differently - all fields will act as they do with other spaces with the exception that:

* `"origin"` and `"mediaType"` are optional. If not supplied they gain placeholder values.
* `"deliveryChannel"` must be `none`, anything other value is rejected.

> [!NOTE]
> Do we want to do this? I'm not sure if different validation logic is better than consumer passing random values? Restricting use of none makes sense.

The API can shortcut assets and auto-finish them, no need to notify Engine at all.

### Routing / Paths

Would we want to output space 0 on output paths? By default we will but it could be useful to make this a configurable option. `{space}` is the path template for space, we could support a `{spaceNot0}` or `{positiveSpace}` to only output the space path component if it is > 0.

> [!NOTE]
> Is this of use? Or would we need to see what path rewrite requirements would be before we determine what to do here?

An alternative might be to have different values based on whether the space is 0 or not. A common customer configuration is to have a Customer/Space per environment, in these instances we would likely want to differentiate between space 0 or not. E.g.

* Canonical image: `https://example.dlcs/iiif-img/2/10/asset`
* Custom image: `https://customer.host/images/asset`
* Canonical asset adjunct: `https://example.dlcs/adjuncts/2/10/asset/mets.xml`
* Custom asset adjunct: `https://customer.host/adjuncts/asset/mets.xml`
* Canonical stub asset adjunct: `https://example.dlcs/adjuncts/2/0/fake/ocr.txt`
* Custom stub asset adjunct: `https://customer.host/other/fake/ocr.txt`

### Space 0 Problem

The problem with using Space 0 is that it has a significant meaning in `CustomerStorage` table. The `CustomerStorage` table with a 0 space means *storage for all spaces*. 

We would need a different mechanism for denoting this - potentially using `null` or an alternative identifier as the "all spaces" space. This is used both for reporting and validating customer hasn't exceeded their storage allowance.

### Manifest Output

Manifest generation will need to support this scenario - it currently won't add any Assets that have `none` channel as there is nothing to deliver. However, there might be some adjuncts to add. In this case we'd need to add a placeholder `Canvas` without any AnnotationPages, only the adjuncts, using existing rules.

## Bulk Adjunct Operations

The API supports bulk adjunct operations at an asset level:
* requesting all adjuncts for an asset `GET /customers/{c}/spaces/{s}/images/{i}/adjuncts` 
* upserting multiple adjuncts to an assets via `POST /customers/{c}/spaces/{s}/images/{i}/adjuncts`

These are useful but in scenarios like IIIF-Presentation where we may want to get all adjuncts for a number of assets, or upsert adjuncts to a number of different assets it would be useful to have alternative endpoints for these.

### Bulk Reading

[AssetQuery](https://dlcs.github.io/public-docs/api-doc/asset-queries/) syntax allows for querying multiple assets based on specified criteria. This is supported on:

* `/customers/{customer}/allImages`
* `/customers/{customer}/spaces/{space}/images`

For bulk reading of adjuncts there are 2 options:

#### "Include" parameter

Extend the query syntax to support an `includes` property - this would include inline adjuncts with each asset. E.g. `/customer/99/allImages?q={{\"manifests\":[\"whatever\"]}}&include=adjuncts`.

* Pro: Single query can fetch all assets and associated adjuncts.
* Pro: For IIIF-Presentation specifically, we are already making this request to get all assets for a manifest. Adding a simple include would allow us to fetch everything in one.
* Con: The returned Image Hydra model has an existing "adjuncts" property, which is normally a URI but this would involve it being a collection of adjuncts.
* Con: It won't be possible to query for adjuncts only.

#### Adjunct Specific

Have an alternative endpoint that supports querying for adjuncts, `/customers/{customer}/allAdjuncts` or `/customers/{customer}/allImages/adjuncts`

* Pro: Would return Hydra collection of Adjuncts.
* Pro: Avoids above issue with Hydra model changes.
* Pro: Allow querying on adjunct types, although this may be of limited use. E.g. `?q={{\"iiifLink\":[\"seeAlso\"]}}`
* Con: We would want to query on parent asset properties, would the syntax need to differ? E.g. `?q={{\"asset.manifests\":[\"whatever\"]}}` to indicate this is the parent Asset property. This would only make sense if we wanted to allow filtering by both asset and adjunct specific values.

### Bulk Writing

`POST /customers/{c}/spaces/{s}/images/{i}/adjuncts` allows bulk adding of adjuncts to a single asset. Could we have an alternative endpoint that would allow adding adjuncts to multiple assets at once? This is akin to how a batch can be created for images in multiple different spaces.

One suggestion would be to `POST /customers/{{customerId}}/adjunct/queue`. Which would create an adjunct batch, similar to asset batch. This would have the same behaviour of an adjunct batch but wouldn't set an `adjunct.batch` property - everything would be managed like the new `BatchAssets` table introduced in [#491](https://github.com/dlcs/protagonist/issues/491).

> [!NOTE]
> Unsure of URL structure below, `/adjunct/queue/` feels cumbersome `/adjunct-queue/` or something different?
>
> Alternatively should we reuse same endpoint as we have now (create a batch of _things_ with different handling for assets and adjuncts).

This would have the same behaviour as an asset batch:
* Raise notification on completion
* GET all batches (equivalent of `/customers/{c}/queue/batches`)
* GET batch details (equivalent of `/customers/{c}/queue/batches/{id}`)
* GET batch images (equivalent of `/customers/{c}/queue/batches/{id}/images`)
* POST add batch (equivalent of `/customers/{c}/queue`)

I don't think we would need all the functionality of batches immediately - no priority batches or recent/active batch endpoints.

> [!NOTE]
> What `@type` would this be? `vocab:Batch` has `"*Images"` properties that are not relevant to adjuncts.
>
> While there is a lot of shared properties, it is a different object. `vocab:AdjunctBatch` perhaps?

### Bulk Deleting

Support bulk delete operations, similar to current POST `/customers/{customerId}/deleteImages`.

## `manifests` Property

Rename `manifests` column/property to `scopes`. `manifests` is to prescriptive for use, it can also be collections too. `scopes` keeps options for reuse open in the future. To fully make this change there will be changes to NQs to support `scopes` parameter, PATCH `/customers/{customerId}/allImages`, update `assetQuery` syntax etc.

The `scopes` parameter is an internal use list of associated references. We will also introduce a `usedBy` property. This renders full URLs to all associated Manifests and Collections that contain this Asset.

## Alternative Options

This section is a record of some alternative approaches that were disregarded.

### Bulk Writing

Have an endpoint that accepts bulk requests for creating adjuncts. This would accept `assetId`, in the same way that batches accept `space`. Instead of returning a batch it would return a Hydra collection of adjuncts.

Without batches there would be no batch completion event, instead we could raise an 'adjuncts for assets' completed.

### Customer Controlled 'special' Space

We could allow customers to specify their own unique 'stub asset' space, controllable by configuration. The additional complexity overhead for this seem unnecessary.

### Adjuncts as Asset-lite

With the above suggestions, adjuncts share a lot of functionality with assets. Would it have been useful to treat them in a very similar way to assets? 

Effectively "file" only assets, with a separate table modelling how they are rendered on generated Manifests?