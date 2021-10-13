
# Space

**UNSTABLE Under active development**

Spaces allow you to partition images into groups. You can use them to organise your images logically, like folders. You can also define different default settings to apply to images registered in a space. For example, default access control behaviour for all images in a space, or default tags. These can be overridden for individual images. There is no limit to the number of images you can register in a space.


```
/customers/{0}/spaces/{1}
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a Space| |vocab:Space|200 OK, 404 Not found|
|PUT|create or replace a Space|vocab:Space|vocab:Space|200 OK, 201 Created Space, 404 Not found|
|PATCH|Update the supplied fields of the Space|vocab:Space|vocab:Space|205 Accepted Space, reset view, 400 Bad request, 404 Not found|
|DELETE|Delete the Space| |owl:Nothing|205 Accepted Space, reset view, 404 Not found|


## Supported properties


### modelId

The internal identifier for the space within the customer (uri component)


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Space|xsd:integer|False|False|


### name

Space name


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Space|xsd:string|False|False|


### created

Date the space was created


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Space|xsd:dateTime|True|False|


### defaultTags

Default tags to apply to images created in this space


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Space|xsd:string|False|False|


### defaultMaxUnauthorised

Default size at which role-based authorisation will be enforced. -1=open, 0=always require auth


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Space|xsd:integer|False|False|


### defaultRoles (🔗)

Default roles that will be applied to images in this space


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Space|hydra:Collection|False|False|


```
/customers/{0}/spaces/{1}/defaultRoles
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves all Role| |hydra:Collection|200 OK|
|POST|Creates a new Role|vocab:Role|vocab:Role|201 Role created., 400 Bad Request|


### images (🔗)

All the images in the space


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Space|hydra:Collection|True|False|


```
/customers/{0}/spaces/{1}/images
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves all Image| |hydra:Collection|200 OK|
|POST|Creates a new Image|vocab:Image|vocab:Image|201 Image created., 400 Bad Request|


### metadata (🔗)

Metadata options for the space


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Space|vocab:Metadata|True|False|


```
/customers/{0}/spaces/{1}/metadata
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve the metadata| |vocab:Metadata|200 OK|


### storage (🔗)

Storage policy for the space


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Space|vocab:CustomerStorage|True|False|


```
/customers/{0}/spaces/{1}/storage
```

