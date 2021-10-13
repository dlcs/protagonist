
# CustomerOriginStrategy

As a customer you can provide information to the DLCS to allow it to fetch your images from their origin endpoints. Every customer has a default origin strategy, which is for the DLCS to attempt to fetch the image from its origin URL without presenting credentials. This is fine for images that are publicly available, but is unlikely to be appropriate for images you are exposing from your asset management system. You might have a service that is available only to the DLCS, or an FTP site. The DLCS has a predefined set of mechanisms for obtaining resources over HTTP, FTP, S3 etc. In your customer origin strategies you match these predefined strategies to regexes that match your origin URLs and credentials that the DLCS can use when requesting your assets.


```
/customers/{0}/originStrategies/{1}
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a Origin Strategy| |vocab:CustomerOriginStrategy|200 OK, 404 Not found|
|PUT|create or replace a Origin Strategy|vocab:CustomerOriginStrategy|vocab:CustomerOriginStrategy|200 OK, 201 Created Origin Strategy, 404 Not found|
|PATCH|Update the supplied fields of the Origin Strategy|vocab:CustomerOriginStrategy|vocab:CustomerOriginStrategy|205 Accepted Origin Strategy, reset view, 400 Bad request, 404 Not found|
|DELETE|Delete the Origin Strategy| |owl:Nothing|205 Accepted Origin Strategy, reset view, 404 Not found|


## Supported properties


### regex

Regex for matching origin. When the DLCS tries to work out how to fetch from your origin, it uses this regex to match to find the correct strategy.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:CustomerOriginStrategy|xsd:string|False|False|


### originStrategy (🔗)

Link to the origin strategy definition that will be used if the regex is matched.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:CustomerOriginStrategy|vocab:OriginStrategy|True|False|


```
/customers/{0}/originStrategies/{1}/originStrategy
```


### credentials (🔗)

JSON object - credentials appropriate to the protocol, will vary. These are stored in S3 and are not available via the API.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:CustomerOriginStrategy|xsd:string|False|False|


```
/customers/{0}/originStrategies/{1}/credentials
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|PUT|create or replace customer credential objedt|vocab:Credentials|vocab:Credentials|201 Created|

