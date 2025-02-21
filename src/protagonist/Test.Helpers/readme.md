# Test Helpers

This project contains any reusable components to help with testing.

## Integration Tests

Helpers for integration tests are in the /Integration folder. 

### Overview

[dotnet-testcontainers](https://github.com/HofmeisterAn/dotnet-testcontainers) is used to manage starting/stopping Postgres and LocalStack containers.

TestContainers is used in 3 fixtures. These fixtures implement xunit's `IAsyncLifetime` and are run as `ICollectionFixture<T>`:

* `DlcsDatabaseFixture` - starts postgres image, instantiates `DlcsContext` and runs migrations. Public properties expose the `ConnectionString` for that instance and `DlcsContext` for managing/verifying data in tests. A random free port will be used and set in the connection string - this is handled by `dotnet-testcontainers`. Customer `99` is setup with a space, origin-strategy etc.
* `LocalStackFixture` -  starts localstack image, configures AWS clients + seeds basic resources (e.g. buckets/queues). Public properties expose AWS clients for use in tests any by `WebApplicationFactory`. Host port "0" is used and will bind to a free port, this is reflected in the `IAmazon*` client instances.
* `StorageFixtures` - this contains the above 2, exposed as public properties. Only one `ICollectionFixture` can be used per test class so this wraps both of the above.

`ProtagonistAppFactory` is a [`WebApplicationFactory`](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1?view=aspnetcore-6.0) that is consumed as a `ClassFixture<T>`. This will set environment to `"Testing"` and use any `appsetting.Testing.json` found in test project. 

This has a couple of fluent helper methods to take connectionString and `LocalStackFixture` to replace any running instances in target `Startup`. Example setup:

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

### Identifiers

Care needs to be taken for the lifecycle of objects setup using CollectionFixtures as these will last for the run of all tests in a class.

One handy way is to use the name of the test in the Asset identifier to avoid collisions.

```cs
[Fact]
public async Task Test_Demonstrates_Thing()
{
    // Arrange
    var idRoot = $"99/1/{nameof(Test_Demonstrates_Thing)}";
    await dbContext.Images.AddTestAsset(AssetId.FromString(idRoot));

    // Test things...
}
```

### Handling Http Dependencies

There are a number of external dependencies that we communicate with via HTTP. These can be handled in 2 different ways:

* Mock Client: if we are using a Typed client we could use `.WithTestServices()` method on `ProtagonistAppFactory` to register a fake/mock version of the dependency and verify that appropriate requests are made. This only tests that calls to the client are correct, the client code would need to be tested separately.
* Api Stub: A library like [Stubbery](https://github.com/markvincze/Stubbery) can be used to control HTTP requests. There are examples of this in the code base but using Stubbery to create mock endpoints allows any error handling etc code to run.

### Troubleshooting

Stopping and starting the docker containers isn't always perfect - particularly if aborting tests without them completing. The following command will delete any of the test-containers running:

```bash
docker rm $(docker stop $(docker ps -q --filter label=protagonist_test))
```