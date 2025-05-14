# PathTemplates

The default path template for requests is `/{prefix}/{version}/{customer}/{space}/{assetPath}`, where:

* `prefix` is route path (e.g. `thumbs`, `iiif-av`, `iiif-img`, `file`)
* `version` is the version slug (e.g. `v2` or `v3`)
* `customer` and `space` are self explanatory
* `assetPath` is the asset identifier plus any specific elements for the current request - e.g. for image requests it will contain the full IIIF image request.

By default the above format is reflected on info.json (from Thumbs and Orchestrator) and generated manifests (single-item and named-query manifests, see [#983](https://github.com/dlcs/protagonist/issues/983)).

To facilitate using proxy servers to receive alternative URLs that are then rewritten to standard DLCS URLs, overrides to the default rules can be specified. These are used when outputting any self-referencing URIs (e.g. info.json `id` element).

## Prefixes

The default prefixes, listed above, can be changed by using `"PrefixReplacements"` key. This is a key-value pair, where the key is the default prefix, and the value is the replacement.

## Output

> [!WARNING]
> As this is a breaking change, feature flag `RewriteAssetPathsOnManifests` needs to be enabled for manifests to contain rewritten paths

In manifests, image and thumb paths can have `{version}` slug as a path replacement values. As the NQ serves as an _"output everything we have"_ skeleton manifest we will always output all supported ImageApi versions. The logic for handling image paths (which are versioned) will be:

* NQ will continue to always output all ImageApi versions (current v2 + v3)
* If we have a `{version}` slug in the config then rewrite both. For canonical the `{version}` is `""`, so it will remove that version.
* If we don't have a `{version}` slug then we'll rewrite paths for canonical ImageApi version but use the standard `/iiif-img/` paths for non-canonical.

## Configuration Examples

> [!IMPORTANT]
> For the below to work the expectation is that the `x-forwarded-host` header is set in the proxy.

```json
"PathRules": {
  "Default": "/{prefix}/{version}/{customer}/{space}/{assetPath}",
  "Overrides": {
    "exclude-space.com": {
      "Path": "/{prefix}/{version}/{customer}/{assetPath}",
      "PrefixReplacements": {
        "iiif-img": "images",
        "file": "bin"
      }
    },
    "customer-specific.io": "/{prefix}/{assetPath}",
    "i-have-ark.io": "/{prefix}/ark:{assetPath:US}"
  }
}
```

Note that it's possible to specify a simple string, or a complex object with both `"Path"` and `"PrefixReplacements"` specified. The following are equivalent in terms of how they are strongly typed:

```json
{
  "string.com": "/{prefix}/{version}/{customer}/{space}/{assetPath}",
  "complex.net": {
    "Path": "/{prefix}/{version}/{customer}/{space}/{assetPath}"
  }
}
```

## Formatters

`assetPath` supports formatting via a known formatting parameter, e.g. `{assetPath}` can be formatted with `{assetPath:FMT}`.

Supported format parameter values are:

* `3US` - replaces triple (`3`) `U`nderscores with `S`lashes (e.g. assetPath `"foo___bar_baz"` -> `"foo/bar_baz"`).

## Auth PathTemplates

There is a similar config block availabe for authentication under the `"Auth"` key for Orchestrator.

For auth the path replacements are simpler:
* `customer` is the customer the auth service is for
* `behaviour` is the name of the auth service.

```json
"Auth": {
  "AuthPathRules": {
    "Default": "/auth/{customer}/{behaviour}",
    "Overrides": {
      "exclude-space.com": "/auth/{behaviour}"
    }
  }
},
```
