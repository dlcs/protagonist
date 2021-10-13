
# ThumbnailPolicy

The settings used to create thumbnails for the image at registration time.


```
/thumbnailPolicies/{0}
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a Thumbnail policy| |vocab:ThumbnailPolicy|200 OK, 404 Not found|


## Supported properties


### name

The human readable name of the image policy


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:ThumbnailPolicy|xsd:string|False|False|


### sizes

The bounding box size of the thumbnails to create. For each of these sizes, a thumbnail will be created. The longest edge of each thumbnail matches this size.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:ThumbnailPolicy|xsd:nonNegativeInteger|False|False|

