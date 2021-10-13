
# Image

The Image resource is the DLCS view of an image that you have registered. The job of the DLCS is to offer services on that image, such as IIIF Image API endpoints. As well as the status of the image, the DLCS lets you store arbitrary metadata that you can use to build interesting applications.


```
/customers/{0}/spaces/{1}/images/{2}
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a Image| |vocab:Image|200 OK, 404 Not found|
|PUT|create or replace a Image|vocab:Image|vocab:Image|200 OK, 201 Created Image, 404 Not found|
|PATCH|Update the supplied fields of the Image|vocab:Image|vocab:Image|205 Accepted Image, reset view, 400 Bad request, 404 Not found|
|DELETE|Delete the Image| |owl:Nothing|205 Accepted Image, reset view, 404 Not found|


## Supported properties


### modelId

The identifier for the image within the space - its URI component. TODO - this shoud not be exposed in the API, use the URI instead?


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:string|False|False|


### infoJson

info.json URI - where the IIIF Image API is exposed for this image


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:string|False|False|


### degradedInfoJson

Degraded info.json URI - if a user does not have permission to view the full image, but a degraded image is permitted, the DLCS will redirect them to this URI.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:string|False|False|


### thumbnailInfoJson

Thumbnail info.json URI


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:string|False|False|


### thumbnail400

Direct URI of the 400 pixel thumbnail


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:string|True|False|


### created

Date the image was added


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:dateTime|True|False|


### origin

Origin endpoint from where the original image can be acquired (or was acquired)


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:string|False|False|


### initialOrigin

Endpoint to use the first time the image is retrieved. This allows an initial ingest from a short term s3 bucket (for example) but subsequent references from an https URI.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:string|False|False|


### maxUnauthorised

Maximum size of request allowed before roles are enforced - relates to the effective WHOLE image size, not the individual tile size. 0 = No open option, -1 (default) = no authorisation


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:integer|False|False|


### width

Tile source width


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:integer|True|False|


### height

Tile source height


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:integer|True|False|


### queued

When the image was added to the queue


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:dateTime|True|False|


### dequeued

When the image was taken off the queue


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:dateTime|True|False|


### finished

When the image processing finished (image ready)


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:dateTime|True|False|


### ingesting

Is the image currently being ingested?


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:boolean|True|False|


### error

Reported errors with this image


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:string|False|False|


### tags

Image tags


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:string|False|False|


### string1

String reference 1


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:string|False|False|


### string2

String reference 2


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:string|False|False|


### string3

String reference 3


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:string|False|False|


### number1

Number reference 1


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:nonNegativeInteger|False|False|


### number2

Number reference 2


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:nonNegativeInteger|False|False|


### number3

Number reference 3


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|xsd:nonNegativeInteger|False|False|


### roles (🔗)

The role or roles that a user must possess to view this image above maxUnauthorised. These are URIs of roles e.g., https://api.dlcs.io/customers/1/roles/requiresRegistration


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|vocab:Role|False|False|


```
/customers/{0}/spaces/{1}/images/{2}/roles
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves all Role| |hydra:Collection|200 OK|
|POST|Creates a new Role|vocab:Role|vocab:Role|201 Role created., 400 Bad Request|


### batch (🔗)

The batch this image was ingested in (most recently). Might be blank if the batch has been archived or the image as ingested in immediate mode.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|vocab:Batch|True|False|


```
/customers/{0}/spaces/{1}/images/{2}/batch
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a Batch| |vocab:Image|200 OK, 404 Not found|
|PUT|create or replace a Batch|vocab:Image|vocab:Image|200 OK, 201 Created Batch, 404 Not found|
|PATCH|Update the supplied fields of the Batch|vocab:Image|vocab:Image|205 Accepted Batch, reset view, 400 Bad request, 404 Not found|
|DELETE|Delete the Batch| |owl:Nothing|205 Accepted Batch, reset view, 404 Not found|


### imageOptimisationPolicy (🔗)

The image optimisation policy used when this image was last processed (e.g., registered)


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|vocab:ImageOptimisationPolicy|True|False|


```
/customers/{0}/spaces/{1}/images/{2}/imageOptimisationPolicy
```


### thumbnailPolicy (🔗)

The thumbnail settings used when this image was last processed (e.g., registered)


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Image|vocab:ThumbnailPolicy|True|False|


```
/customers/{0}/spaces/{1}/images/{2}/thumbnailPolicy
```

