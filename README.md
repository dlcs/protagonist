# Protagonist

(WIP) A collection of separate dotnet core applications that together form the basis of the new DLCS platform.

## About

* [What is the DLCS?](docs/what-is-dlcs-io.md)
* [Interaction Patterns](https://github.com/dlcs/protagonist/issues?q=is%3Aissue+label%3A%22Interaction+Pattern%22+sort%3Acreated-asc)
* [Architectural Considerations](docs/architectural-considerations.md)
* [Design Principles](docs/rfcs/006-Design-Principles.md)
* [RFCs](docs/rfcs)
* [Architecture Diagram](https://raw.githubusercontent.com/dlcs/protagonist/master/docs/c4-container-diagrams/DLCS-2020-l2.png)

## Projects

There are a number of shared projects and entry point applications that use these shared projects, detailed below:

### Shared

* DLCS.Core - general non-domain specific utilities and exceptions.
* DLCS.Model - DLCS models and repository interfaces.
* DLCS.Repository - Repository implementations.
* DLCS.Web - Classes that are aware of HTTP pipeline (e.g. request/response classes)
* IIIF - For parsing and processing IIIF requests.

In addition to the above there are a number of *.Tests classes for automated tests.

### Entry Points

* Thumbs- simplified handling of thumbnail requests.