
# CustomerStorage

Information resource that shows the current storage use for a Customer or for anindividual Space within a customer.


```
/customers/{0}/storage, /customers/{0}/spaces/{1}/storage
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a CustomerStorage| |vocab:CustomerStorage|200 OK, 404 Not found|


## Supported properties


### numberOfStoredImages

Number of stored images


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:CustomerStorage|xsd:integer|True|False|


### totalSizeOfStoredImages

Total storage usage for images excluding thumbnails, in bytes


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:CustomerStorage|xsd:integer|True|False|


### totalSizeOfThumbnails

Total storage usage for thumbnails, in bytes


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:CustomerStorage|xsd:integer|True|False|


### lastCalculated

When the DLCS last evaluated storage use to generate this resource


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:CustomerStorage|xsd:dateTime|True|False|


### storagePolicy (🔗)

When the customer storage resource is for a Customer rather than a space, itwill include this property which configures the total storage permitted across all a Customer's spaces. 


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:CustomerStorage|vocab:StoragePolicy|True|False|


```
/customers/{0}/storage, /customers/{0}/spaces/{1}/storage/storagePolicy
```

