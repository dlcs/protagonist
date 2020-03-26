# Protagonist

(WIP) A collection of separate dotnet core applications that together form the basis of the new DLCS platform.

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