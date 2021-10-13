
# PortalRole

**UNSTABLE Under consideration.**

A role that can be assigned to a user of the DLCS portal (not an end user) for the customer to allow control over permissions.


```
/portalRoles/{0}
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a Portal Role| |vocab:PortalRole|200 OK, 404 Not found|


## Supported properties


### name

The human readable name of the origin strategy


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:PortalRole|xsd:string|False|False|

