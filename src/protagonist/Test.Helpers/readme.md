# Test Helpers

This project contains any reusable components to help with testing.

## Integration Tests

Helpers for integration tests are in the /Integration folder. 

### Overview

[dotnet-testcontainers](https://github.com/HofmeisterAn/dotnet-testcontainers) is used to manage starting/stopping Postgres and LocalStack containers.

TestContainers is used in 3 fixtures. These fixtures implement xunit's `IAsyncLifetime` and are run as `ICollectionFixture<T>`:

* `DlcsDatabaseFixture` - starts postgres image, instantiates `DlcsContext` and runs migrations. Public properties expose the `ConnectionString` for that instance and `DlcsContext` for managing/verifying data in tests. A random free port will be used and set in the connection string - this is handled by `dotnet-testcontainers`.
* `LocalStackFixture` -  starts localstack image, configures AWS clients + seeds basic resources (e.g. buckets/queues). Public properties expose AWS clients for use in tests any by `WebApplicationFactory`. Host port "0" is used and will bind to a free port, this is reflected in the `IAmazon*` client instances.
* `StorageFixtures` - this contains the above 2, exposed as public properties. Only one `ICollectionFixture` can be used per test class so this wraps both of the above.

`ProtagonistAppFactory` is a [`WebApplicationFactory`](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1?view=aspnetcore-5.0) that is run as a `ClassFixture<T>`. This has a couple of fluent helper methods to take connectionString and `LocalStackFixture` to replace any running instances in target `Startup`:

```cs
[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class ExampleTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;
    private readonly IAmazonS3 amazonS3;

    public ExampleTests(ProtagonistAppFactory<Startup> factory, StorageFixture storageFixture)
    {
        // Store dbFixture to read/write in test assertions/setup
        dbFixture = storageFixture.DbFixture;

        // Store amazonS3 client configured to use LocalStack instance
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        
        // Configure WebApplicationFactory
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithLocalStack(storageFixture.LocalStackFixture)
            .WithConfigValue("Overridden-AppSetting", "Overwrites other app setting value")
            .WithTestServices(services =>
            {
                services.AddSingleton<IService, TestService>();
            })
            .CreateClient(new WebApplicationFactoryClientOptions {AllowAutoRedirect = false});
        
        // Clean down database
        dbFixture.CleanUp();
    }
```

> Care needs to be taken for the lifecycle of objects setup using CollectionFixtures as these will last for the run of all tests in a class.

#### Troubleshooting

Stopping and starting the docker containers isn't always perfect - particularly if aborting tests without them completing. The following command will delete any of the test-containers running:

```bash
docker rm $(docker stop $(docker ps -q --filter label=protagonist_test))
```

### TODO

The tests here are pretty basic and just prove that we can control the relevant classes.

* Still need to handle requests to DLCS, which are all done via `HttpClient` so should be reasonably straight forward.
* Managing database and S3 elements.