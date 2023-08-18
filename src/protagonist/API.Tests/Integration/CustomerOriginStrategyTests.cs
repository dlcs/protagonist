using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;
using API.Tests.Integration.Infrastructure;
using DLCS.Model.Customers;
using DLCS.Repository;
using Test.Helpers;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class CustomerOriginStrategyTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private readonly DlcsContext dlcsContext;
    private readonly IAmazonS3 s3Client;

    public CustomerOriginStrategyTests(StorageFixture storageFixture, ProtagonistAppFactory<Startup> factory)
    {
        dlcsContext = storageFixture.DbFixture.DbContext;
        s3Client = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(storageFixture.DbFixture, "API-Test",
            f => f.WithLocalStack(storageFixture.LocalStackFixture));
        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task Get_CustomerOriginStrategy_200()
    {
        // Arrange
        const int customerId = 90;
        var strategy = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "http[s]?://(.*).example.com",
            Strategy = OriginStrategyType.BasicHttp,
            Credentials = @"{""user"": ""test-user"",
                             ""password"": ""test-password""}",
            Optimised = false,
            Order = 1
        };
        var path = $"customers/{customerId}/originStrategies/{strategy.Id}";

        await dlcsContext.CustomerOriginStrategies.AddAsync(strategy);
        await dlcsContext.SaveChangesAsync();

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_CustomerOriginStrategy_201()
    {
        // Arrange
        const int customerId = 91;
        var path = $"customers/{customerId}/originStrategies";

        const string newStrategyJson = @"{
            ""strategy"": ""s3-ambient"",
            ""regex"": ""my-regex"",
            ""order"": ""2""
        }";

        // Act
        var content = new StringContent(newStrategyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var foundStrategy = dlcsContext.CustomerOriginStrategies.Single(s => s.Customer == customerId);
        foundStrategy.Strategy.Should().Be(OriginStrategyType.S3Ambient);
        foundStrategy.Regex.Should().Be("my-regex");
        foundStrategy.Order.Should().Be(2);
    }

    [Fact]
    public async Task Post_CustomerOriginStrategy_201_WithCredentials()
    {
        // Arrange
        const int customerId = 92;
        var path = $"customers/{customerId}/originStrategies";

        const string newStrategyJson = @"{
            ""strategy"": ""basic-http-authentication"",
            ""credentials"": ""{\""user\"": \""user-example\"", \""password\"": \""password-example\""}"",
            ""regex"": ""my-regex"",
            ""order"": ""2""
        }}";

        // Act
        var content = new StringContent(newStrategyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var foundStrategy = dlcsContext.CustomerOriginStrategies.Single(s => s.Customer == customerId);
        foundStrategy.Strategy.Should().Be(OriginStrategyType.BasicHttp);
        foundStrategy.Regex.Should().Be("my-regex");
        foundStrategy.Credentials.Should().NotBeEmpty();
        foundStrategy.Order.Should().Be(2);
        foundStrategy.Optimised.Should().BeFalse();

        var storedCredentials = await s3Client.GetObjectAsync(LocalStackFixture.SecurityObjectsBucketName,
            $"{customerId}/origin-strategy/{foundStrategy.Id}/credentials.json");
        storedCredentials.ResponseStream.GetContentString().Should()
            .Be(@"{""user"":""user-example"",""password"":""password-example""}");
    }

    [Fact]
    public async Task Post_CustomerOriginStrategy_409_IfRegexAlreadyExists()
    {
        // Arrange
        const int customerId = 93;
        var path = $"customers/{customerId}/originStrategies";
        var existingStrategy = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "my-regex",
            Strategy = OriginStrategyType.BasicHttp
        };
        const string newStrategyJson = @"{
            ""strategy"": ""basic-http-authentication"",
            ""credentials"": ""{\""user\"": \""user-example\"", \""password\"": \""password-example\""}"",
            ""regex"": ""my-regex"",
            ""order"": ""2""
        }";

        await dlcsContext.CustomerOriginStrategies.AddAsync(existingStrategy);
        await dlcsContext.SaveChangesAsync();

        // Act
        var content = new StringContent(newStrategyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_CustomerOriginStrategy_400_IfOptimisedWithoutS3AmbientStrategy()
    {
        // Arrange
        const int customerId = 94;
        var path = $"customers/{customerId}/originStrategies";

        const string newStrategyJson = @"{
            ""strategy"": ""basic-http-authentication"",
            ""regex"": ""my-regex"",
            ""optimised"": ""true"",
            ""order"": ""2""
        }";

        // Act
        var content = new StringContent(newStrategyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_CustomerOriginStrategy_200()
    {
        // Arrange
        const int customerId = 95;

        var strategy = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "http[s]?://(.*).example.com",
            Strategy = OriginStrategyType.S3Ambient,
            Optimised = true,
            Order = 1
        };
        const string newStrategyJson = @"{
            ""strategy"": ""basic-http-authentication"", 
            ""credentials"": ""{ \""user\"": \""user-updated\"", \""password\"": \""password-updated\"" }"",
            ""regex"": ""http[s]?://(.*).example2.com"",
            ""optimised"": ""false"",
            ""order"": ""2""
        }";
        var path = $"customers/{customerId}/originStrategies/{strategy.Id}";

        await dlcsContext.CustomerOriginStrategies.AddAsync(strategy);
        await dlcsContext.SaveChangesAsync();

        // Act
        var content = new StringContent(newStrategyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var foundStrategy = dlcsContext.CustomerOriginStrategies.Single(s => s.Id == strategy.Id);
        foundStrategy.Strategy.Should().Be(OriginStrategyType.BasicHttp);
        foundStrategy.Regex.Should().Be("http[s]?://(.*).example2.com");
        foundStrategy.Optimised.Should().BeFalse();
        foundStrategy.Order.Should().Be(2);

        var storedCredentials = await s3Client.GetObjectAsync(LocalStackFixture.SecurityObjectsBucketName,
            $"{customerId}/origin-strategy/{foundStrategy.Id}/credentials.json");
        storedCredentials.ResponseStream.GetContentString().Should()
            .Be(@"{""user"":""user-updated"",""password"":""password-updated""}");
    }
    
    [Fact]
    public async Task Put_CustomerOriginStrategy_409_IfRegexAlreadyExists()
    {
        // Arrange
        const int customerId = 95;

        var strategyA = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "http[s]?://(.*).example.com",
            Strategy = OriginStrategyType.BasicHttp,
            Optimised = false,
            Order = 1
        };
        var strategyB = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "http[s]?://(.*).other-example.com",
            Strategy = OriginStrategyType.S3Ambient,
            Optimised = true,
            Order = 2
        };
        const string newStrategyJson = @"{
            ""strategy"": ""basic-http-authentication"", 
            ""credentials"": ""{ \""user\"": \""user-updated\"", \""password\"": \""password-updated\"" }"",
            ""regex"": ""http[s]?://(.*).other-example.com"",
            ""optimised"": ""false"",
            ""order"": ""2""
        }";
        var path = $"customers/{customerId}/originStrategies/{strategyA.Id}";

        await dlcsContext.CustomerOriginStrategies.AddRangeAsync(strategyA, strategyB);
        await dlcsContext.SaveChangesAsync();

        // Act
        var content = new StringContent(newStrategyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var foundStrategy = dlcsContext.CustomerOriginStrategies.Single(s => s.Id == strategyB.Id);
        foundStrategy.Strategy.Should().Be(strategyB.Strategy);
        foundStrategy.Regex.Should().Be(strategyB.Regex);
        foundStrategy.Optimised.Should().Be(strategyB.Optimised);
        foundStrategy.Order.Should().Be(strategyB.Order);
    }
}