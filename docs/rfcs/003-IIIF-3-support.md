# Upgrade IIIF versions

* Named queries can project Presentation 3
* Proper AV support in projections
* Image API 3
* Auth 0.9.3 => 1

The legacy version of the DLCS supported IIIF 2.1 only for both the Presentation and Image API.

It uses the netstandard [Digirati.IIIF](https://www.nuget.org/packages/Digirati.IIIF/) (https://github.com/digirati-co-uk/iiif-model) library to model build IIIF models.

There is a new dotnet 5 implementation of IIIF models, [iiif-net](https://www.nuget.org/packages/iiif-net/), which is based on models used for the [iiif-builder](https://github.com/wellcomecollection/iiif-builder) project. It will also include a converter that produces IIIF 2.1 from this model, rather than offer extensive support for both models.

## Handling Different Versions

The DLCS will present different paths that control which API version is to be returned (see below).

They will also present a canonical path that _doesn't_ have a version specified in the URL. This path will return the default IIIF API version set via configuration, or it can be controlled via content negotiation.

## Presentation API

The `orchestrator` components of DLCS will output IIIF Presentation resources for single-item manifests and named queries. The paths are:

Single-item manifests [#183](https://github.com/dlcs/protagonist/issues/183)
* `/iiif-manifest/{customer}/{space}/{image}`  - Canonical URL.
* `/iiif-manifest/v2/{customer}/{space}/{image}` - PresentationApi 2.1.
* `/iiif-manifest/v3/{customer}/{space}/{image}` - PresentationApi 3.0.

Named-query results [#175](https://github.com/dlcs/protagonist/issues/175)
* `/iiif-resource/{customer}/{space}/{image}` - Canonical URL.
* `/iiif-resource/v2/{customer}/{space}/{image}` - PresentationApi 2.1.
* `/iiif-resource/v3/{customer}/{space}/{image}` - PresentationApi 3.0.

All required classes are supported in [iiif-net](https://www.nuget.org/packages/iiif-net/).

## Image API

The `orchestrator` and `thumbs` components output IIIF Image API for info.json requests [#247](https://github.com/dlcs/protagonist/issues/247)

Orchestrator:
* `/iiif-img/{cust}/{space}/{iiif-image-request}/info.json` - Canonical URL.
* `/iiif-img/v2/{customer}/{space}/{image}/info.json` - ImageApi v2.1
* `/iiif-img/v3/{customer}/{space}/{image}/info.json` - ImageApi v3

Thumbs:
* `/thumbs/{cust}/{space}/{iiif-image-request}/info.json` - Canonical URL.
* `/thumbs/v2/{customer}/{space}/{image}/info.json` - ImageApi v2.1
* `/thumbs/v3/{customer}/{space}/{image}/info.json` - ImageApi v3

The [iiif-net](https://www.nuget.org/packages/iiif-net/) library will need expanded to handle Image v3 classes.