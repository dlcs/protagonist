# PathTemplates

The default path template for requests is `/{prefix}/{version}/{customer}/{space}/{assetPath}`, where:

* `prefix` is route path (e.g. `iiif-manifest`, `iiif-av`, `iiif-img`)
* `version` is the version slug (e.g. `v2` or `v3`)
* `customer` and `space` are self explanatory
* `assetPath` is the asset identifier plus any specific elements for the current request - e.g. for image requests it will contain the full IIIF image request.

By default the above format is reflected on info.json (from Thumbs and Orchestrator).

To facilitate using proxy servers to receive alternative URLs that are then rewritten to standard DLCS URLs, overrides to the default rules can be specified. These are used when outputting any self-referencing URIs (e.g. info.json `id` element).

> [!IMPORTANT]
> For the below to work the expectation is that the `x-forwarded-host` header is set in the proxy.

```
"PathRules": {
  "Default": "/{prefix}/{version}/{customer}/{space}/{assetPath}",
  "Overrides": {
    "exclude-space.com": "/{prefix}/{customer}/extra/{assetPath}/",
    "customer-specific.io": "/{prefix}/{assetPath}"
    "i-have-ark.io": "/{prefix}/ark:{assetPath:US}"
  }
}
```

As an convenience you can specify `"PathRules:OverridesAsJson"` appSetting, for Orchestrator only, that includes a string-based config. This makes it easier to configure via environment variables etc

## Formatters

`assetPath` supports formatting via a known formatting parameter, e.g. `{assetPath}` can be formatted with `{assetPath:FMT}`.

Supported format parameter values are:

* `3US` - replaces triple (`3`) `U`nderscores with `S`lashes (e.g. assetPath `"foo___bar_baz"` -> `"foo/bar_baz"`).

## Auth PathTemplates

There is a similar config block availabe for authentication under the `"Auth"` key for Orchestrator.

For auth the path replacements are simpler:
* `customer` is the customer the auth service is for
* `behaviour` is the name of the auth service.

```
"Auth": {
  "AuthPathRules": {
    "Default": "/auth/{customer}/{behaviour}",
    "Overrides": {
      "exclude-space.com": "/auth/{behaviour}"
    }
  }
},
```
