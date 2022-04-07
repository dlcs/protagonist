# Orchestrator

Orchestrator is a reverse-proxy that handles requests for all assets (image, timebased, files), description resources (info.json, single item manifests and named-query projections) and PDF generation.

## Technology :robot:

* [YARP](https://microsoft.github.io/reverse-proxy/) - native dotnet reverse-proxy.
* [Dapper](https://github.com/DapperLib/Dapper) - high performance object mapper, prefered in place of EF for performance.

## YARP Routes

In addition to standard controller route handling the following YARP configuration is used:

### Custom Handling

The below routes are handled by custom logic and YARP's [Direct Forwarding](https://microsoft.github.io/reverse-proxy/articles/direct-forwarding.html) behaviour:

#### `/iiif-img/{customer}/{space}/{image}/{**assetRequest}`

Handle image asset requests. Will:

* Validate access for restricted assets.
* Proxy `thumbs` for known thumbnail sizes.
* Proxy `thumbsresize` for requests that can be served by resizing thumbs.
* Ensure asset copied to fast disk and proxy `image-server` for other sizes and tile requests. Image requests will have "CustomHeader" headers appended as required.

Decision logic in `ImageRequestHandler` and routing logic in `ImageRouteHandler`. 

#### `/iiif-av/{customer}/{space}/{image}/{**assetRequest}`

Handle requests for TimeBased assets. Will:

* Validate access for restricted assets.
* Redirect to s3 for open assets.
* Proxy s3 for restricted assets.

Decision logic in `TimeBasedRequestHandler` and routing logic in `TimeBasedRouteHandlers`. 

### Standard Proxied Routes

The following routes are defined for YARP to handle:

* img_options - handles any `OPTIONS` requests for images, proxied to deliverator
* av_infojson - handles `GET` requests for timebased media `info.json` requests, proxied to deliverator
* av_only - handles `GET` requests for `/iiif-av/{cust}/{space}/{image}` requests, proxied to deliverator as these are info.json requests (without info.json)
* av_options - handles any `OPTIONS` requests for images `/iiif-av/`, proxied to deliverator
* fallback - handles any requests, for any verb, that are not `iiif-img` or `iiif-av` requests.

> Note - YARP is initially acting as a replacement to NGINX in legacy DLCS, most of these routes will be removed in time.

## Clusters

The following YARP Clusters are used:

* deliverator - legacy DLCS orchestrator implementation.
* image_server - IIIF image-server.
* thumbs - Protagonist thumbs service.
* thumbsresize - Protagonist thumbs service, configured to resize thumbs on the fly.

> Note: `thumbs` and `thumbsresize` can reference the same uri if required.

## Deployment

See `Dockerfile.Orchestrator` in the solution root for deployment artifacts.

```bash
cd..
docker build -f Dockerfile.Orchestrator -t orchestrator:local .
```

## Known Issues

* Proxying authenticated TimeBased media fails when running locally (S3 permissions issue).