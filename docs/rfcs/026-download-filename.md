# Controlling Download Filename

See [#1024](https://github.com/dlcs/protagonist/issues/1024).

## Brief

When a user downloads a full image served by DLCS, e.g. via the UV download dialog, the file is saved with the last
segment of the IIIF Image API path. This is almost always `default.jpg`, so downloading several images results in
`default.jpg`, `default (1).jpg`, `default (2).jpg` etc.

Multiple customers have asked to be able to control the filename that images are downloaded as. This RFC outlines an
opt-in mechanism, via a query parameter, that results in Orchestrator setting a `Content-Disposition` response header.

### Why not `<a download>`?

The HTML [`download`](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/a#download) attribute allows a
client to suggest a filename. However, browsers ignore it for cross-origin URLs, which is the typical case for
DLCS-hosted images embedded in another site (unless the image is fetched and re-served as a `blob:` URL). A
`Content-Disposition` header set by the server works regardless of origin.

## Proposal

Add an optional `filename` query parameter to `/iiif-img/` image requests. The value is a _template_ made up of
`{placeholder}` tokens that are resolved from the requested asset.

| Request                                                     | Result                                                            |
|-------------------------------------------------------------|-------------------------------------------------------------------|
| `/iiif-img/2/1/foo/full/max/0/default.jpg`                  | Unchanged, no `Content-Disposition` header                        |
| `/iiif-img/2/1/foo/full/max/0/default.jpg?filename={id}`    | `Content-Disposition: attachment; filename=2_1_foo.jpg`           |
| `/iiif-img/2/1/foo/full/max/0/default.jpg?filename=bar`     | Unchanged, unsupported value is ignored                           |

> [!NOTE]
> `{` and `}` should be URL-encoded by clients (`?filename=%7Bid%7D`). Both encoded and unencoded forms are handled.

### Why a template?

Rough notes on the issue suggested `?filename=id` for a keyword and `?filename=string3` for a literal value. Mixing keywords 
and literal values in the same parameter is ambiguous - it would be impossible to request a file literally named "id", 
and every new keyword risks clashing with an existing literal value. A `{placeholder}` syntax avoids this and leaves room
for composite values in the future (e.g. `{reference1}-{id}`).

### Initial scope

The initial implementation will only support a value of exactly `{id}` (case-insensitive). Anything else, including
other placeholders, literal text or mixed templates, is ignored. This fixes the shape of the parameter now without
committing to rules for literal text or additional placeholders before they are needed.

* `{id}` resolves to `{customer}_{space}_{image}`. `/` is not valid in filenames, so it is replaced with `_`.
* The file extension is always taken from the IIIF Image Request format (e.g. `.jpg`, `.png`). An image id that already
  has an extension will have a second one appended (e.g. `foo.tiff` -> `2_1_foo.tiff.jpg`).
* The header value is generated using `ContentDispositionHeaderValue.SetHttpFileName`, which outputs both `filename`
  and `filename*` ([RFC 6266](https://www.rfc-editor.org/rfc/rfc6266)) so non-ASCII identifiers are handled correctly.
* Only `/iiif-img/` image requests are affected. `info.json`, `/thumbs/`, `/file/` and `/iiif-av/` are out of scope.

## Implementation

Orchestrator it follows the same pattern as [custom headers](https://github.com/dlcs/protagonist/blob/develop/src/protagonist/Orchestrator/Features/Images/CustomHeaderProcessor.cs):

* `ImageRequestHandler` builds a `ProxyActionResult` for the downstream destination (thumbs, resize-thumbs, image-server
  or special-server). After setting custom headers, if `?filename=` is a supported template a `Content-Disposition`
  header is added to the result. This overrides any `Content-Disposition` custom header configured for the customer, as
  the explicit request takes precedence.
* `PathRewriteTransformer` only copies `ProxyActionResult.Headers` to the response when the downstream response is
  successful, so error responses never include `Content-Disposition`.
* `PathRewriteTransformer` does not forward the query string to downstream services, so `?filename=` never reaches
  thumbs, image-server or special-server. No downstream changes are required.
* The header is added after auth checks have been completed, so there are no access-control implications.

No database migrations or configuration changes are required.

## Concerns

### CDN caching

Image responses are `Cache-Control: public`. Any intermin cache layer (ie CloudFront) will need to include the `filename` 
query parameter in its cache key, or a response with `Content-Disposition: attachment` could be cached and served for plain
requests (forcing a download in viewers), or vice versa.

**The CloudFront cache policy for `/iiif-img/` must include `filename` in the cache key.** This is an infrastructure
change required alongside the code change.

### Cache fragmentation

Each distinct `filename` value results in a separate CDN cache entry and origin request. For full-size images served
by special-server this means repeat renders. Restricting values to a fixed set of placeholders keeps this bounded;
allowing free-text literal values would not.

### Arbitrary filenames

If arbitrary literal values were supported (e.g. `?filename=whatever`) it would allow anyone to craft a link on the DLCS
domain that downloads with an attacker-chosen name, e.g. `invoice.pdf.exe`. The content would still be an image but the
name, combined with a trusted domain, lends credibility (similar to a
[Reflected File Download](https://www.trustwave.com/en-us/resources/blogs/spiderlabs-blog/reflected-file-download-a-new-web-attack-vector/)).

Mitigations, if literal values are ever supported:

* Always force the extension from the IIIF Image Request format.
* Strip path separators, control characters and other unsafe characters.
* Prefer values derived from the asset over caller-supplied values.

### Discoverability

Viewers will not add `?filename=` themselves - e.g. the UV constructs download URLs from the image service. For this to
be useful, either:

* the Manifest includes a [`rendering`](https://iiif.io/api/presentation/3.0/#rendering) link for each Canvas that
  includes the parameter, or
* the viewer is updated to append the parameter.

Manifests generated by DLCS (e.g. single-asset manifests, named queries) could add these `rendering` links in future
but that isn't part of this proposal.

### IIIF Image API

Adding query parameters to IIIF Image API requests is outside of the spec. It doesn't conflict with it as the parameter
doesn't affect the image returned, only how a browser treats the response. The canonical URL is unchanged.

### CORS

`Access-Control-Expose-Headers` is not changed. This is only required if a client reads `Content-Disposition` via
`fetch`/XHR; navigation and link downloads work without it.

## Next Steps

### Additional placeholders

Filenames based on other asset properties, e.g. `{reference1}` or `{numberReference1}`.

The cached `OrchestrationImage` is deliberately lean and doesn't contain these values. `DapperAssetRepository` already
selects `Reference1-3` and `NumberReference1-3` from the database but these are dropped when mapping. Options:

1. **On-demand lookup (preferred)** - only when the template contains a placeholder other than `{id}`, make a separate
   query to fetch the required value(s). Download requests are rare relative to tile requests so the additional DB hit
   is acceptable. Placeholders map to columns via a whitelist; user input is never interpolated into SQL. The result
   could optionally be cached under its own short-lived key to avoid repeat lookups without increasing the size of
   `OrchestrationImage`.
2. **Widen `OrchestrationImage`** - simplest, but every cached asset carries the extra values for a rarely used
   feature.
3. **Caller-supplied literal values** - no DB lookup as the Manifest author already knows the metadata, but has the
   security concerns outlined above.

When multiple placeholders are supported, rules are needed for:

* Unknown placeholders - ignore or `400 Bad Request`?
* Placeholders that resolve to an empty value - fallback to `{id}`?
* Literal text between placeholders - allowed characters and maximum length.

### Other delivery channels

`/file/` and `/iiif-av/` could support the same parameter, though `/file/` responses may already contain a
`Content-Disposition` header from S3 object metadata.

## Open Questions

* Should an unsupported `filename` value be ignored (proposed) or return `400 Bad Request`?
* Is `{customer}_{space}_{image}.{format}` the preferred format for `{id}`, or should it be `{image}.{format}` only?
* Should DLCS-generated Manifests advertise download links with this parameter?
