
# Role

A role is used by the DLCS to enforce access control. Images have roles.The DLCS acquires a user's roles from a RoleProvider. In the case of the simple 'clickthrough' role, the DLCS can supply this role to the user, but in other scenarios the DLCS needs to acquire roles for the user from the customer's endpoints.


```
/customers/{0}/roles/{1}
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a Role| |vocab:Role|200 OK, 404 Not found|
|PUT|create or replace a Role|vocab:Role|vocab:Role|200 OK, 201 Created Role, 404 Not found|
|PATCH|Update the supplied fields of the Role|vocab:Role|vocab:Role|205 Accepted Role, reset view, 400 Bad request, 404 Not found|
|DELETE|Delete the Role| |owl:Nothing|205 Accepted Role, reset view, 404 Not found|


## Supported properties


### name

The role name - this might be the same as the ID?


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Role|xsd:string|False|False|


### label

Label for a slightly longer description of the role


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Role|xsd:string|False|False|


### aliases

If the DLCS acquires roles from the customer, they might have different names, or change over time. This allows a customer to release one role name via a roleprovider but use a different name within the DLCS.


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Role|xsd:string|False|False|


### authService (🔗)

The IIIF Auth Service for this role


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Role|vocab:AuthService|False|False|


```
/customers/{0}/roles/{1}/authService
```


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a Auth Service| |vocab:Role|200 OK, 404 Not found|
|PUT|create or replace a Auth Service|vocab:Role|vocab:Role|200 OK, 201 Created Auth Service, 404 Not found|
|PATCH|Update the supplied fields of the Auth Service|vocab:Role|vocab:Role|205 Accepted Auth Service, reset view, 400 Bad request, 404 Not found|
|DELETE|Delete the Auth Service| |owl:Nothing|205 Accepted Auth Service, reset view, 404 Not found|

