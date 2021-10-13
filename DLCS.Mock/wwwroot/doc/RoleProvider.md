
# RoleProvider

**UNSTABLE Under active development**

Resource that represents the means by which the DLCS acquires roles to enforce an access control session. The DLCS maintains the session, but needs an external auth service (CAS, OAuth etc) to authenticate the user and acquire roles. The RoleProvider contains the configuration information required by the DLCS to interact with a customer's endpoint. The credentials used during the interaction are stored in S3 and not returned via the API.


```
/customers/{0}/authServices/{1}/roleProvider
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a Role Provider| |vocab:RoleProvider|200 OK, 404 Not found|
|PUT|create or replace a Role Provider|vocab:RoleProvider|vocab:RoleProvider|200 OK, 201 Created Role Provider, 404 Not found|
|PATCH|Update the supplied fields of the Role Provider|vocab:RoleProvider|vocab:RoleProvider|205 Accepted Role Provider, reset view, 400 Bad request, 404 Not found|
|DELETE|Delete the Role Provider| |owl:Nothing|205 Accepted Role Provider, reset view, 404 Not found|


## Supported properties


### configuration

JSON configuration blob for this particular service


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:RoleProvider|xsd:string|False|False|


### credentials

Credentials - not exposed via API, but can be written to by customer.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:RoleProvider|xsd:string|False|True|

