# Integration Tests

The files container here will eventually be moved out to reused by the wider solution but starting with Portal for now.

## Overview

[dotnet-testcontainers](https://github.com/HofmeisterAn/dotnet-testcontainers) is used to manage starting/stopping Postgres and LocalStack containers.

TestContainers is used in 3 fixtures. These fixtures implement xunit's `IAsyncLifetime` and are run as `ICollectionFixture<T>`:

* `DlcsDatabaseFixture` - starts postgres image, instantiates `DlcsContext` and runs migrations. Public properties expose the `ConnectionString` for that instance and `DlcsContext` for managing/verifying data in tests.
* `LocalStackFixture` -  starts localstack image, configures AWS clients + seeds basic resources (e.g. buckets/queues). Public properties expose AWS clients for use in tests any by `WebApplicationFactory`.
* `StorageFixtures` - this contains the above 2, exposed as public properties. Only one `ICollectionFixture` can be used per test class so this wraps both.

`ProtagonistAppFactory` is a [`WebApplicationFactory`](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1?view=aspnetcore-5.0) that is run as a `ClassFixture<T>`. This has a couple of methods to take connectionString and `LocalStackFixture` to replace any running instances in target `Startup`.

> TODO - figure a better way for managing how these are set?

## Authentication

`ProtagonistAppFactory` adds a new `AuthenticationHandler` to make it easier to make authenticated requests. It's currently Portal specific in that it will add a portal user. 2 extension methods for `HttpClient` are used to make it easier to make requests:

* `AsCustomer(this HttpClient client, int customer = 2)` - if specified gives "Customer" role. `httpClient.AsAdmin().GetAsync("/admin");`
* `AsAdmin(this HttpClient client, int customer = 2)` - if specified gives Admin role (in addition to "Customer"). `httpClient.AsCustomer(4).GetAsync("/admin");`

This may need to differ between different services and may need to be moved.


### Troubleshooting

Stopping and starting the docker containers isn't always perfect - particularly if aborting tests without them completing. The following command will delete any of the test-containers running:

```bash
docker rm $(docker stop $(docker ps -q --filter label=protagonist_test))
```

## TODO

The tests here are pretty basic and just prove that we can control the relevant classes.

* Still need to handle requests to DLCS, which are all done via `HttpClient` so should be reasonably straight forward.
* Have a cleaner way of setting up fixtures + `WebApplicationFactory`
* Managing database and S3 elements.
* Verify this runs okay in Jenkins.