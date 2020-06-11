# Named Queries

## Overview

An image can have arbitrary metadata associated with it (e.g. strings and integer values), that can be set as part of ingest or later via a PATCH request. The DLCS doesn't put any meaning to these values, this is determined by the consuming application. The DLCS gives means of filtering and querying by these values.

One of these means is a "named query" - this is a method to select from images where one of the metadata values of the image matches some value, then order by one of the other metadata values. The DLCS can return this query as a IIIF Manifest, or a PDF.

The named query has 2 parts:

* What data should be included in the projection - including ordering and grouping.
* How the data should be expressed - PDF, IIIF 2.1 Manifest, IIIF 3.0 Presentation Manifest, MOBI, ePUB etc?

## An Example

A named query is specially formatted string that defines a template and mapping arguments to url parameters.

For example, if we had a named-query named "foo", that was defined as:

`manifest=s1&sequence=n1&canvas=n2&s1=p1&n1=p2&space=p3`

we could call this with:

```
                             nq-name    p1  p2  p3
                                   \    |   |  /  
https://dlcs.io/iiif-resource/test/foo/bar/15/7
```

which would end up as:

`manifest=s1&sequence=n1&canvas=n2&s1=bar&n1=15&space=7`

Which would be translated by the internal parser to a query that can materialise the requested images. These can then be translated into a IIIF Manifest, PDF, ePUB, MOBI etc as required.

For a full explanation of all currently available syntax, please see the [Named Query API](https://dlcs-book.readthedocs.io/en/latest/More_on_named_queries.html) docs.


## Proposed

* Projections: IIIF 2, 3; PDF
* Revisit the named query syntax, and the tools to build named queries in the DLCS portal
* Pass in link to IIIF Manifest that _uses_ the Image API endpoints; PDF projection reads the text content of canvases and includes it in the PDF (Fireball enhancement) => proper text in PDF
* Research need for other projections like ePUB, MOBI, etc.
* Non image based - correctly handle.

Note that there is a class of named queries that doesn't require that the DLCS hosts the images, but that might not be a named query any more.

## Suggested named query URL form (public view)

In the current orchestrator, a named query conforms to 

`/(pdf|iiif-resource)/customer/named-query-name/{*params}`

where:

- first slot is identified by the orchestrator as a named query - we could add more top level query types later, for other projection types
- last slot is as many /p1/p2/p3... as you want to feed it

This could be better.

`/q/customer/named-query-name/{*params}`

...where q indicates that this is a named query of _any_ type, always routed to Orchestrator
Orchestrator then works out what the handler is by looking up the name in the third segment (e.g., `/my-pdf-query/`)

Orchestrator maintains a map of customer, queries, handler, so it can forward it on to (potentially) separate services, e.g., a PDF generator, a Manifest generator.

It would make for more useful URLs. It would also mean you weren't allowed to have two different types of query with the same name in the same customer, but that's OK. That's good.

The problem with a current URL like https://dlcs.digirati.io/pdf/2/eden/space-test/1 is "what do I call it?"

`/pdf/.../pdf/`

Instead, you can just have /q/2/pdf/... (or whatever we choose as the "named query" signifier in the first path segment, doesn't have to be `/q/`). At the moment we have to keep  reserving words in the first path segment.
