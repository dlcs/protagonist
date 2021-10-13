
# NamedQuery

**UNSTABLE Currently the named query implementation is a placeholder,**

A named query is a URI pattern available on dlcs.io (i.e., not this API) that will return a IIIF resource such as a collection, or manifest, or sequence, or canvas. For example:

```
https://dlcs.io/resources/iiifly/manifest/43/ae678999
```

This query is is an instance of the following template:

```
https://dlcs.io/resources/{customer}/{named-query}/{space}/{string1}
```

This customer (iiifly) has a named query called 'manifest' that takes two parameters - the space and the string1 metadata field. The query is internally defined to use an additional field - number1 -  and to generate a manifest with one sequence, with each canvas in the sequence having one image. The images selected by the query must all have string1=ae678999 in this case, and are ordered by number1.  An image query against the dlcs API returns a collection of DLCS Image objects. a Named Query uses an DLCS image query but then projects these images and  constructs a IIIF resource from them, using the parameters provided. Information on designing and configuring named queries is provided in a special topic.


```
/customers/{0}/namedQueries/{1}
```


## Supported operations


|Method|Label|Expects|Returns|Statuses|
|--|--|--|--|--|
|GET|Retrieve a Named Query| |vocab:NamedQuery|200 OK, 404 Not found|
|PUT|create or replace a Named Query|vocab:NamedQuery|vocab:NamedQuery|200 OK, 201 Created Named Query, 404 Not found|
|PATCH|Update the supplied fields of the Named Query|vocab:NamedQuery|vocab:NamedQuery|205 Accepted Named Query, reset view, 400 Bad request, 404 Not found|
|DELETE|Delete the Named Query| |owl:Nothing|205 Accepted Named Query, reset view, 404 Not found|


## Supported properties


### name

The name that appears for the query in the path on https://dlcs.io, e.g., 'manifest'


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:NamedQuery|xsd:string|False|False|


### global

The named query is available to all customers


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:NamedQuery|xsd:boolean|False|False|


### template

URI template


|domain|range|readonly|writeonly|
|--|--|--|--|
|vocab:NamedQuery|xsd:string|False|False|

