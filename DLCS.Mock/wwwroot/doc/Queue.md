
# Queue

The Queue resource allows the DLCS to process very large number of image registration requests.You can post a Collection of images to the Queue for processing (a Hydra collection, see note). This results in the creation of a Batch resource. You can then retrieve these batches to monitor the progress of your images.


```
/customers/{0}/queue
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Returns the queue resource| |vocab:Queue| |
|POST|Submit an array of Image and get a batch back|hydra:Collection|vocab:Batch|201 Job has been accepted - Batch created and returned|


## Supported properties


### size

Number of total images in your queue, across all batches


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Queue|xsd:nonNegativeInteger|True|False|


### batches (🔗)

Collection (paged) of the batches - the separate jobs you have submitted to the queue


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Queue|hydra:Collection|True|False|


```
/customers/{0}/queue/batches
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves all batches for customer| |hydra:Collection| |


### images (🔗)

Collection (paged). Merged view of images on the queue, across batches. Typically you'd use this to look at the top or bottom of the queue (first or large page).


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Queue|hydra:Collection|True|False|


```
/customers/{0}/queue/images
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves all images across batches for customer| |hydra:Collection| |


### recent (🔗)

Collection (paged) of finished batches which are not marked as superseded. Most recent first.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Queue|hydra:Collection|True|False|


```
/customers/{0}/queue/recent
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves the recent (non superseded) batches for customer.| |hydra:Collection| |


### active (🔗)

Collection (paged) of batches that are currently in process.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Queue|hydra:Collection|True|False|


```
/customers/{0}/queue/active
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves the customer's currently running batches.| |hydra:Collection| |

