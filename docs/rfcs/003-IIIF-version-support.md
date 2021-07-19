# IIIF Version Support

The DLCS can serve multiple different versions of IIIF resources. It doesn't store the data in any one particular format but instead contains enough information to project it to the required format.

The 'default' version can be specified at various levels - by orgUnit/customer, space or by individual manifest.

This default version will be served at a canonical URL. However, this canonical URL will also support serving different resources by using content negotiation.

The DLCS also supports versioned URLs to explicitly request a versioned resource without needing to resort to content negotiation.

* Named queries can project Presentation 3
* Proper AV support in projections (named queries)
* Image API 3
* Auth 0.9.3 => 1

## Implementation

Our work on `/thumbs/` has started a new version of https://github.com/digirati-co-uk/iiif-model

We will extend this to make a nice IIIF Library for .NET Core.

This can succeed the existing iiif-model library. It will natively use Presentation 3 for fluent building of IIIF, even more pleasantly than the current one.

It will also include a converter that produces IIIF 2.1 from this model, rather than offer extensive support for both models.
