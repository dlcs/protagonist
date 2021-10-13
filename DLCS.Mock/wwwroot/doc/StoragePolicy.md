
# StoragePolicy

A resource that acts as configuration for a customer or space. It is linked to from the storage resource for any customer or space. 


```
/storagePolicies/{0}
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a Storage policy| |vocab:StoragePolicy|200 OK, 404 Not found|


## Supported properties


### maximumNumberOfStoredImages

The maximum number of images that can be registered, across ALL the Customer's spaces


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:StoragePolicy|xsd:nonNegativeInteger|False|False|


### maximumTotalSizeOfStoredImages

The DLCS requires storage capacity to service the images registred by customers. This setting governs how much capacity the DLCS can use for a Customer across all the customer's spaces. Capacity is affected by image optimsation policy (higher quality = more storage used) and the absolutesize of the images (pixel dimensions).


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:StoragePolicy|xsd:nonNegativeInteger|False|False|

