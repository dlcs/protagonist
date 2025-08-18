# Protagonist

A collection of separate dotnet core applications that together form the basis of the new DLCS platform.

## About

* [What is the DLCS?](docs/what-is-dlcs-io.md)
* [Interaction Patterns](https://github.com/dlcs/protagonist/issues?q=is%3Aissue+label%3A%22Interaction+Pattern%22+sort%3Acreated-asc)
* [Architectural Considerations](docs/architectural-considerations.md)
* [Design Principles](docs/rfcs/006-Design-Principles.md)
* [RFCs](docs/rfcs)
* [Architecture Diagram](https://raw.githubusercontent.com/dlcs/protagonist/master/docs/c4-container-diagrams/DLCS-2023-l2.png)
* [Public API Documentation](https://dlcs-book.readthedocs.io/en/latest/)

## Projects

There are a number of shared projects and entry point applications that use these shared projects, detailed below:

### Shared

* DLCS.AWS - classes for interacting with AWS resources.
* DLCS.Core - general non-domain specific utilities and exceptions.
* DLCS.HydraModel - [Hydra](https://www.hydra-cg.com/) models for DLCS API.
* DLCS.Mediatr - shared classes for projects using [Mediatr](https://github.com/jbogard/MediatR).
* DLCS.Model - DLCS models and repository interfaces.
* DLCS.Mock - Mock version of API serving pre-canned responses from memory.
* DLCS.Repository - Repository implementations and `DbContext` for database.
* DLCS.Web - Classes that are aware of HTTP pipeline (e.g. request/response classes)
* Hydra - Base classes for Hydra objects (not DLCS specific).

In addition to the above there are a number of `*.Tests` classes for automated tests.

### Entry Points

* API - HTTP API for interactions.
* Engine - asset ingestion/derivative creation.
* Orchestrator - reverse proxy that serves user requests.
* Portal - administration UI for managing assets.
* Thumbs - simplified handling of thumbnail requests.
* CleanupHandler - monitors queue for notifications + deletes asset derivatives on receipt.
* Migrator - Applies any pending EF migrations.

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

The main entry point is [`run_build.yml`](.github/workflows/run_build.yml). This runs `dotnet test` then uses the parameterised `docker-build-and-push` files to handle Docker image creation.

PRs to `main`, `develop`, pushes to `main`, `develop` and `v*` tags will:
* Build + test dotnet code
* Build and push docker containers 
  * This won't happen for draft PRs unless `build-image` label is added

Pushes to `main`, `develop` and `v*` tags will also run sonar analysis.

## Configuration

Multiple services use `ForwardedHeadersMiddleware` to listen for `X-Forwarded-Host` and `X-Forwarded-Proto`.

By default these only allow these headers to be set by requests from a `KnownNetwork` or `KnownHost` (see [`ForwardedHeaderMiddleware`](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-8.0#forwarded-headers-middleware-options) docs for more options).

To add to the default values, set `"KnownNetwork"` config setting, this should be set to a comma-delimited list of CIDR values.

Applicable for `API`, `Thumbs` and `Orchestrator` services.

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

Both docker-compose files will spin up a Postgres, [LocalStack](https://github.com/localstack/localstack) container and external resources like image-servers etc. Postgres connection details are specified via `.env` file (see `.env.dist` for example) and this is listening on `:5452`. The LocalStack image will contain required resources, see [seed-resources.sh](./compose/localstack/seed-resources.sh) and these will be used by DLCS for S3 access.

Using the connection and AWS details from `.env.dist` and `appsettings.Development.Example.json` will work by default. The `seed-resources.sh` file will seed AWS resources and EFMigrations will be run on Orchestrator startup is `"RunMigrations"` appsetting is `true`.

> Note that full stack cannot be run until all elements have been sufficiently implemented.

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

> Note that the seed data added by `TestData` is insufficient to fully run DLCS and will need expanded.

Migrations are added using:

```bash
dotnet ef migrations add "Table gains column" -p DLCS.Repository -s API
```
if you would like to view the SQL the migration will produce, you can use the following command:

```bash
dotnet ef migrations script -i -o .\migrate.sql -p DLCS.Repository -s API
```