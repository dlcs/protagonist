
# PortalUser

A user of the portal. Represents an account for use by a person, rather than by a machine. You can create as many portal user accounts as required. Note that the roles a portal user has relate to DLCS permissions rather than permissions on your image resources.


```
/customers/{0}/portalUsers/{1}
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a Portal User| |vocab:PortalUser|200 OK, 404 Not found|
|PUT|create or replace a Portal User|vocab:PortalUser|vocab:PortalUser|200 OK, 201 Created Portal User, 404 Not found|
|PATCH|Update the supplied fields of the Portal User|vocab:PortalUser|vocab:PortalUser|205 Accepted Portal User, reset view, 400 Bad request, 404 Not found|
|DELETE|Delete the Portal User| |owl:Nothing|205 Accepted Portal User, reset view, 404 Not found|


## Supported properties


### email

The email address


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:PortalUser|xsd:string|False|False|


### created

Create date


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:PortalUser|xsd:dateTime|False|False|


### roles (🔗)

Collection of Role resources that the user has. These roles should not be confused with the roles associated with images and authservices, which govern the interactions that end users can have with your image resources. These PortalUser roles govern the actions that your handful of registered DLCS back end users can perform in the portal. 


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:PortalUser|hydra:Collection|True|False|


```
/customers/{0}/portalUsers/{1}/roles
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieves all Portal Role| |hydra:Collection|200 OK|
|POST|Creates a new Portal Role|vocab:PortalRole|vocab:PortalRole|201 Portal Role created., 400 Bad Request|


### enabled

Whether the user can log in - for temporary or permanent rescinding of access.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:PortalUser|xsd:boolean|False|False|

