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
* DLCS.Mediatr - shared classes for projects using [Mediatr](https://github.com/jbogard/MediatR).
* DLCS.Model - DLCS models and repository interfaces.
* DLCS.Repository - Repository implementations and `DbContext` for database.
* DLCS.Web - Classes that are aware of HTTP pipeline (e.g. request/response classes)

In addition to the above there are a number of `*.Tests` classes for automated tests.

### Entry Points

* Thumbs - simplified handling of thumbnail requests.
* Orchestrator - reverse proxy that serves user requests (WIP).
* Portal - administration UI for managing assets (WIP).
* API - HTTP API for interactions (WIP).

## Technology :robot:

There are a variety of technologies used across the projects, including:

* [LazyCache](https://github.com/alastairtree/LazyCache) - lazy in-memory cache.
* [Serilog](https://serilog.net/) - structured logging framework.
* [Mediatr](https://github.com/jbogard/MediatR) - mediator implementation for in-proc messaging.
* [EFCore](https://github.com/dotnet/efcore) - ORM data-access and migrations.
* [Dapper](https://github.com/DapperLib/Dapper) - high performance object mapper.
* [XUnit](https://xunit.net/) - automated test framework.

## Deployment

[Github actions](.github/workflows) are used to build and push new Docker images to github container registry.

The main entry point is [`run_build.yml`](.github/workflows/run_build.yml). This runs `dotnet test` then uses the parameterised `build_docker.yml` files to handle Docker image creation.