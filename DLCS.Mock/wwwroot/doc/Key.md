
# Key

Credentials for accessing the API. The Key object will only have the accompanying secret field returned once, when a new key is created. Thereafter only the key is available from the API.


```
/customers/{0}/keys/{1}
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Returns keys allocated to this customer resource| |vocab:Key| |
|POST|Submit an empty POST and the DLCS will generate a key and secret. Requires eleveated |owl:Nothing|vocab:Key|201 Job has been accepted - key created and returned|


## Supported properties


### key

API Key


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Key|xsd:string|False|False|


### secret

API Secret (available at creation time only)


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:Key|xsd:string|False|False|

