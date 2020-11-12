# Design Principles

_(goes with [What is the DLCS](../what-is-dlcs-io.md))_

You give the DLCS an asset (e.g., an image), it provides a delivery endpoint (e.g., a IIIF Image Service) for it. You can influence its behaviour with default- and per-image settings (thumbnail sizes, access control and more).

The DLCS is almost always going to be a component of a wider system. It needs _your_ code to do something useful for your users.

It is designed for _integration_, whether from a browser-based content creation tool, or a complex digitisation workflow.

It should not overreach its responsibility. It's never going to be a Discovery environment, but your discovery environment uses it. 

We think that delivery of _assets_ (the Image API, but also AV derivatives) is a separate concern from the delivery of Manifests (and other Presentation API resources such as Collections and Annotation Pages). One system should not be responsible for both, they should not be coupled together. There are many more ways to produce a Manifest than there are to provide an Image Service. 

We also think that web-scale delivery of assets to the public is a very separate concern from _preservation_. It might seem natural that, since the preservation system is where all the master assets live, it is in the right system to deliver access to those assets via web derivatives that it creates. But preservation has very different usage and scaling characteristics from systems for providing asset delivery with wide public visibility. 

The DLCS should make it easy to forget about the technical details, scaling and even the formats of asset delivery. It can do this because it can implement standards (IIIF in particular) for these services. 

As well as direct asset delivery via derivatives (where the IIIF Image API is considered to be a derivative), the DLCS is well placed to handle _some_ derived aggregations, whether for performance reasons (like making PDFs from hundreds of images, where it has the images at source) or for convenience, such as generating skeleton IIIF Manifests from named queries. These are examples of happy secondary opportunities on top of the primary asset delivery purpose.

The DLCS stores metadata about its assets for _your_ use - that is, for systems integration and synchronisation, you can use the DLCS to store some arbitrary additional metadata, and query on it. Although the DLCS treats assets as atomic units, it can be queried to generate aggregations in useful formats, whether for end-user consumption, or for systems integration purposes.

## Use cases

As a development platform, DLCS is informed by two different types of use case.

1) Developer use cases - that inform a powerful and easy to use API. How do I write nice-looking code against this _platform_?
2) End user use cases - the human needs that these developers are trying to meet with help from the DLCS: user interactions with digital objects. What patterns of _load_ do they generate?

The DLCS cannot and should not know about all possible examples of 2). But its design is informed by common usage scenarios in its target market (e.g., digitised cultural heritage collections). Its architecture must allow us to accomodate newly emerging interactions that generate different load patterns, and optimise for typical consumption.

* See [The Shape of Traffic](006-appendix-shape-of-traffic.md)
* See [Interaction Patterns](https://github.com/dlcs/protagonist/issues?q=is%3Aissue+label%3A%22Interaction+Pattern%22) in GitHub

## Technical Implementation

(This applies to the eventual fully re-worked version)

The DLCS is a mixture of [.NET 5](https://dotnet.microsoft.com/) (C# 9) and Python 3.9, with PostgreSQL, Redis and various AWS-provided infrastructure for storage, queues etc.

It is licensed under MIT; code is at https://github.com/dlcs/protagonist.

The DLCS codebase must be readily understandable to a competent .NET developer. That is, it should be _vanilla_ .NET 5. This doesn't mean it can't use third party tools and frameworks, as long as they are already in widespread use in the .NET community. Dapper is an example of a third party framework that meets this definition - something any developer is likely to encounter on multiple projects.

While it is not currently designed to be platform-agnostic (it makes use of specific AWS features), this decision is based on cost not architecture. A variation that runs on Azure, or on-premise hardware, is possible if funded.

It should be locally runnable. That doesn't mean the entire stack needs to be local, but typical moving parts for development and debugging should be designed to support simply running from the IDE or local command line. For example, everything local except S3 and queues; everything local except S3, queues, Redis and Postgres as local Docker containers, etc.