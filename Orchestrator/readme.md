# Orchestrator

Orchestrator is a reverse-proxy that handles requests for all assets (image, timebased and files).

## Technology :robot:

* [YARP](https://microsoft.github.io/reverse-proxy/) - native dotnet reverse-proxy.
* [Dapper](https://github.com/DapperLib/Dapper) - high performance object mapper, prefered in place of EF for performance.

## Routes

### Custom Handling

The following routes are handled by custom logic and YARP's [Direct Forwarding](https://microsoft.github.io/reverse-proxy/articles/direct-forwarding.html) behaviour:

#### `/iiif-img/{customer}/{space}/{image}/{**assetRequest}`

Handle image requests. Will redirect to `/thumbs/` for known thumbnails and handle authentication.

#### `/iiif-av/{customer}/{space}/{image}/{**assetRequest}`

Handle requests for TimeBased assets. Handles authentication and proxies media files from S3.

### Standard Proxied Routes

The following routes are defined for YARP to handle:

* img_options - handles any `OPTIONS` requests for images, proxied to deliverator
* av_infojson - handles `GET` requests for timebased media `info.json` requests, proxied to deliverator
* av_only - handles `GET` requests for `/iiif-av/{cust}/{space}/{image}` requests, proxied to deliverator as these are info.json requests (without info.json)
* av_options - handles any `OPTIONS` requests for images `/iiif-av/`, proxied to deliverator
* fallback - handles any requests, for any verb, that are not `iiif-img` or `iiif-av` requests.

> Note - YARP is initially acting as a replacement to NGINX in legacy DLCS, most of these routes will be removed in time.

## Clusters

3 YARP Clusters are used:

* deliverator - legacy DLCS orchestrator implementation.
* image_server - IIIF image-server.
* thumbs - Protagonist thumbs service.

## Deployment

See Dockerfile.Orchestrator in the solution root for deployment artifacts.

```bash
cd..
docker build -f Dockerfile.Orchestrator -t orchestrator:local .
```

## Known Issues

* Proxying authenticated TimeBased media fails when running locally (S3 permissions issue).