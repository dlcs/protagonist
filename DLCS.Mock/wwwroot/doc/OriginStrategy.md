
# OriginStrategy

**UNSTABLE Under active development**

As a customer you can provide information to the DLCS to allow it to fetch your images from their origin endpoints. Every customer is given a default origin strategy, which is for the DLCS to attempt to fetch the image from its origin URL without presenting credentials. This is fine for images that are publicly available, but is unlikely to be appropriate for images you are exposing from your asset management system. You might have a service that is available only to the DLCS, or an FTP site.  


```
/originStrategies/{0}
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a Origin Strategy| |vocab:OriginStrategy|200 OK, 404 Not found|


## Supported properties


### name

The human readable name of the origin strategy


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:OriginStrategy|xsd:string|False|False|


### requiresCredentials

Whether the DLCS needs stored credentials to fetch images with this strategy


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:OriginStrategy|xsd:boolean|False|False|

