# Protagonist

(WIP) A collection of separate dotnet core applications that together form the basis of the new DLCS platform.

## About

* [What is the DLCS?](docs/what-is-dlcs-io.md)
* [Interaction Patterns](https://github.com/dlcs/protagonist/issues?q=is%3Aissue+label%3A%22Interaction+Pattern%22+sort%3Acreated-asc)
* [Architectural Considerations](docs/architectural-considerations.md)
* [Design Principles](docs/rfcs/006-Design-Principles.md)
* [RFCs](docs/rfcs)
* [Architecture Diagram](https://raw.githubusercontent.com/dlcs/protagonist/master/docs/c4-container-diagrams/DLCS-2021-l2.png)
* [Public API Documentation](https://dlcs-book.readthedocs.io/en/latest/)

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

## Getting Started

There are 2 docker-compose files:

* `docker-compose.local.yml` - this will start any required external dependencies to enable local development (see below).
* `docker-compose.yml` - this spins up a full DLCS stack, including dotnet components.

```bash
# start full stack
docker-compose up

# start external dependencies only for local dev
docker-compose -f docker-compose.local.yml up
```

### Local Development

Both docker-compose files will spin up a Postgres, [LocalStack](https://github.com/localstack/localstack) container and external resources like image-servers etc. Postgres connection details are specified via `.env` file (see `.env.dist` for example) and this is listening on `5452`. The LocalStack image will contain required resources, see [seed-resources.sh](./compose/localstack/seed-resources.sh) and these will be used by DLCS for S3 access.

Using the connection and AWS details from `.env.dist` and `appsettings.Development.Example.json` will work by default. The `seed-resources.sh` file will seed AWS resources and EFMigrations will be run on Orchestrator startup is `"RunMigrations"` appsetting is `true`.

#### LocalStack 

Use of LocalStack can be controlled by appsettings:

```json
{
    "AWS": {
        "Region" : "us-east-1",
        "Profile": "Used for running against AWS",
        "UseLocalStack": true,
        "S3": {
            "ThumbsBucket": "dlcs-thumbs"
        }
    }
}
```

Use helpers from `DLCS.AWS` rather than AWS SDK for registering dependencies, e.g.:

```cs
// Use built in helpers
services
  .SetupAws(configuration, webHostEnvironment)
  .WithAmazonS3();

// Rather than default SDK methods
services
  .AddDefaultAWSOptions(configuration.GetAWSOptions())
  .AddAWSService<IAmazonS3>()
```

If `environment.IsDevelopment() && awsSettings.UseLocalStack;` when the above will register amazon sdk dependencies using LocalStack rather than AWS.

```bash
# view thumbs bucket
aws s3 ls dlcs-thumbs --recursive --human-readable --endpoint-url=http://localhost:4566
```

#### Database

Running Orchestrator with appSetting `RunMigrations = true` will apply migrations to DB on startup.

Running the `TestData` app will seed data to database.

```bash
dotnet run ./Utils/TestData/TestData.csproj
```

> Note that the seed data added by `TestData` is insufficient to fully run DLCS and will need expanded as Engine and API are ported.