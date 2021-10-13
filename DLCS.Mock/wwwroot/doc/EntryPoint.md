
# EntryPoint

The main entry point or homepage of the API.


```

```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|The API's main entry point.| |vocab:EntryPoint| |


## Supported properties


### customers (🔗)

List of customers to which you have access (usually 1)


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:EntryPoint|hydra:Collection|True|False|


```
/customers
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves all Customer entities| |hydra:Collection| |


### originStrategies (🔗)

List of available origin strategies that the DLCS can use to fetch your images.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:EntryPoint|hydra:Collection|True|False|


```
/originStrategies
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves all availabe origin strategies. You must use one of these @id URIs as the OriginStrategy property of any CustomerOriginStrategy resources you create.| |hydra:Collection| |


### portalRoles (🔗)

List of all the different roles available to portal users - i.e., the small number of people who log into the portal. These are not the same as the roles end users acquire for accessing protected image services.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:EntryPoint|hydra:Collection|True|False|


```
/portalRoles
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves all available portal roles. You can add these to the 'roles' collection of any portal users you create.| |hydra:Collection| |


### imageOptimisationPolicies (🔗)

List of available optimisation policies the DLCS uses to process your image to provide a IIIF endpoint. We keep a record of the policy used to allow a different policy (e.g., better quality) to be used later.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:EntryPoint|hydra:Collection|True|False|


```
/imageOptimisationPolicies
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves the policies the DLCS can use or has used in the past to optimise your origin image for IIIF delivery| |hydra:Collection| |


### thumbnailPolicies (🔗)

List of all the different roles available to portal users - i.e., the small number of people who log into the portal. These are not the same as the roles end users acquire for accessing protected image services.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:EntryPoint|hydra:Collection|True|False|


```
/thumbnailPolicies
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves available thumbnail polices - a record of the thumbnails created for an image.| |hydra:Collection| |


### storagePolicies (🔗)

Available storage policies that can be associated with a Customer or a Space. They determine the number of images and storage capacity permitted to the Customer or Space.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:EntryPoint|hydra:Collection|True|False|


```
/storagePolicies
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves available storage polices - maximum image count and storage usage.| |hydra:Collection| |

