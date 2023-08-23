using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;
using API.Client;
using API.Tests.Integration.Infrastructure;
using DLCS.Model.Customers;
using DLCS.Repository;
using Hydra.Collections;
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
    public async Task Get_CustomerOriginStrategies_200()
    {
        // Arrange
        const int customerId = 88;
        
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
        var path = $"customers/{customerId}/originStrategies";

        await dlcsContext.CustomerOriginStrategies.AddRangeAsync(strategyA,strategyB);
        await dlcsContext.SaveChangesAsync();

        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        var model = await response.ReadAsHydraResponseAsync<HydraCollection<DLCS.HydraModel.CustomerOriginStrategy>>();
     
        // Assert
        
       model.Members.Should().OnlyContain(s => s.Credentials == "xxx", @"credentials should be hidden with ""xxx"" in the returned collection");
       model.Members.Should().HaveCount(2);
       response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task Get_CustomerOriginStrategy_200()
    {
        // Arrange
        const int customerId = 89;
        
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
        var model = await response.ReadAsHydraResponseAsync<DLCS.HydraModel.CustomerOriginStrategy>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        model.Credentials.Should().Be("xxx", @"credentials should be hidden with ""xxx"" in the returned object");
    }
    
    [Fact]
    public async Task Get_CustomerOriginStrategy_404_IfNotFound()
    {
        // Arrange
        const int customerId = 90;
        var path = $"customers/{customerId}/originStrategies/{Guid.Empty}";
        
        // Act
        var response = await httpClient.AsCustomer(customerId).GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_CustomerOriginStrategy_201()
    {
        // Arrange
        const int customerId = 91;
        const string newStrategyJson = @"{
            ""strategy"": ""s3-ambient"",
            ""regex"": ""http[s]?://(.*).example.com"",
            ""order"": ""2""
        }";
        
        var path = $"customers/{customerId}/originStrategies";

        // Act
        var content = new StringContent(newStrategyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var foundStrategy = dlcsContext.CustomerOriginStrategies.Single(s => s.Customer == customerId);
        foundStrategy.Strategy.Should().Be(OriginStrategyType.S3Ambient);
        foundStrategy.Regex.Should().Be("http[s]?://(.*).example.com");
        foundStrategy.Order.Should().Be(2);
    }

    [Fact]
    public async Task Post_CustomerOriginStrategy_201_WithCredentials()
    {
        // Arrange
        const int customerId = 92;
        const string newStrategyJson = @"{
            ""strategy"": ""basic-http-authentication"",
            ""credentials"": ""{\""user\"": \""user-example\"", \""password\"": \""password-example\""}"",
            ""regex"": ""http[s]?://(.*).example.com"",
            ""order"": ""2""
        }}";
        
        var path = $"customers/{customerId}/originStrategies";
        
        // Act
        var content = new StringContent(newStrategyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var foundStrategy = dlcsContext.CustomerOriginStrategies.Single(s => s.Customer == customerId);
        foundStrategy.Strategy.Should().Be(OriginStrategyType.BasicHttp);
        foundStrategy.Regex.Should().Be("http[s]?://(.*).example.com");
        foundStrategy.Credentials.Should().NotBeEmpty();
        foundStrategy.Order.Should().Be(2);
        foundStrategy.Optimised.Should().BeFalse();
        foundStrategy.Credentials.Should().Be($"s3://{LocalStackFixture.SecurityObjectsBucketName}/{customerId}/origin-strategy/{foundStrategy.Id}/credentials.json");
        
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
        const string newStrategyJson = @"{
            ""strategy"": ""basic-http-authentication"",
            ""credentials"": ""{\""user\"": \""user-example\"", \""password\"": \""password-example\""}"",
            ""regex"": ""http[s]?://(.*).example.com"",
            ""order"": ""2""
        }";
        
        var path = $"customers/{customerId}/originStrategies";
        var existingStrategy = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "http[s]?://(.*).example.com",
            Strategy = OriginStrategyType.BasicHttp
        };
  
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
        const string newStrategyJson = @"{
            ""strategy"": ""basic-http-authentication"",
            ""credentials"": ""{\""user\"": \""user-example\"", \""password\"": \""password-example\""}"",
            ""regex"": ""http[s]?://(.*).example.com"",
            ""optimised"": ""true"",
            ""order"": ""2""
        }";
        
        var path = $"customers/{customerId}/originStrategies";

        // Act
        var content = new StringContent(newStrategyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Post_CustomerOriginStrategy_400_IfCredentialsUsedWithoutBasicHttpAuth()
    {
        // Arrange
        const int customerId = 95;
        const string newStrategyJson = @"{
            ""strategy"": ""s3-ambient"",
            ""credentials"": ""{\""user\"": \""user-example\"", \""password\"": \""password-example\""}"",
            ""regex"": ""http[s]?://(.*).example.com"",
            ""optimised"": ""true"",
            ""order"": ""2""
        }";

        var path = $"customers/{customerId}/originStrategies";
     
        // Act
        var content = new StringContent(newStrategyJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData(@"""{\""user\"": \""\"", \""password\"": \""password-example\""}""")]
    [InlineData(@"""{\""user\"": \""user-example\"", \""password\"": \""\""}""")]
    [InlineData(@"""{\""username\"": \""user-example\"", \""pass\"": \""password-example\""}""")]
    [InlineData(@"""{}""")]
    public async Task Post_CustomerOriginStrategy_400_IfCredentialsInvalid(string credentialsJson)
    {
        // Arrange
        const int customerId = 96;
        
        var path = $"customers/{customerId}/originStrategies";
        var newStrategyJson = $@"{{
            ""strategy"": ""basic-http-authentication"",
            ""credentials"": {credentialsJson},
            ""regex"": ""http[s]?://(.*).example.com"",
            ""order"": ""2""
        }}";

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
        const int customerId = 97;
        const string strategyChangesJson = @"{
            ""regex"": ""http[s]?://(.*).example2.com"",
            ""optimised"": ""false"",
            ""order"": ""2""
        }";
        
        var strategy = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "http[s]?://(.*).example.com",
            Strategy = OriginStrategyType.S3Ambient,
            Optimised = true,
            Order = 1
        };
        var path = $"customers/{customerId}/originStrategies/{strategy.Id}";

        await dlcsContext.CustomerOriginStrategies.AddAsync(strategy);
        await dlcsContext.SaveChangesAsync();

        // Act
        var content = new StringContent(strategyChangesJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var foundStrategy = dlcsContext.CustomerOriginStrategies.Single(s => s.Id == strategy.Id);
        foundStrategy.Strategy.Should().Be(OriginStrategyType.S3Ambient);
        foundStrategy.Regex.Should().Be("http[s]?://(.*).example2.com");
        foundStrategy.Optimised.Should().BeFalse();
        foundStrategy.Order.Should().Be(2);
    }
    
    [Fact]
    public async Task Put_CustomerOriginStrategy_200_WithCredentials()
    {
        // Arrange
        const int customerId = 98;
        const string strategyChangesJson = @"{
            ""strategy"": ""basic-http-authentication"", 
            ""credentials"": ""{ \""user\"": \""user-updated\"", \""password\"": \""password-updated\"" }"",
            ""regex"": ""http[s]?://(.*).example2.com"",
            ""optimised"": ""false"",
            ""order"": ""2""
        }";
        
        var strategy = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "http[s]?://(.*).example.com",
            Strategy = OriginStrategyType.S3Ambient,
            Optimised = true,
            Order = 1
        };
        var path = $"customers/{customerId}/originStrategies/{strategy.Id}";

        await dlcsContext.CustomerOriginStrategies.AddAsync(strategy);
        await dlcsContext.SaveChangesAsync();

        // Act
        var content = new StringContent(strategyChangesJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var foundStrategy = dlcsContext.CustomerOriginStrategies.Single(s => s.Id == strategy.Id);
        foundStrategy.Strategy.Should().Be(OriginStrategyType.BasicHttp);
        foundStrategy.Regex.Should().Be("http[s]?://(.*).example2.com");
        foundStrategy.Optimised.Should().BeFalse();
        foundStrategy.Order.Should().Be(2);
        foundStrategy.Credentials.Should().Be($"s3://{LocalStackFixture.SecurityObjectsBucketName}/{customerId}/origin-strategy/{foundStrategy.Id}/credentials.json");

        var storedCredentials = await s3Client.GetObjectAsync(LocalStackFixture.SecurityObjectsBucketName,
            $"{customerId}/origin-strategy/{foundStrategy.Id}/credentials.json");
        storedCredentials.ResponseStream.GetContentString().Should()
            .Be(@"{""user"":""user-updated"",""password"":""password-updated""}");
    }
    
    [Fact]
    public async Task Put_CustomerOriginStrategy_409_IfRegexAlreadyExists()
    {
        // Arrange
        const int customerId = 99;
        const string newStrategyJson = @"{
            ""strategy"": ""basic-http-authentication"", 
            ""credentials"": ""{ \""user\"": \""user-updated\"", \""password\"": \""password-updated\"" }"",
            ""regex"": ""http[s]?://(.*).other-example.com"",
            ""optimised"": ""false"",
            ""order"": ""2""
        }";
        
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
    
    [Fact]
    public async Task Put_CustomerOriginStrategy_400_IfOptimisedWithoutS3AmbientStrategy()
    {
        // Arrange
        const int customerId = 100;
        const string strategyChangesJson = @"{
            ""optimised"": ""true""
        }";
        
        var strategy = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "http[s]?://(.*).example.com",
            Strategy = OriginStrategyType.BasicHttp,
            Order = 1
        };
        var path = $"customers/{customerId}/originStrategies/{strategy.Id}";
        
        await dlcsContext.CustomerOriginStrategies.AddAsync(strategy);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var content = new StringContent(strategyChangesJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData(@"""{\""user\"": \""\"", \""password\"": \""password-example\""}""")]
    [InlineData(@"""{\""user\"": \""user-example\"", \""password\"": \""\""}""")]
    [InlineData(@"""{\""username\"": \""user-example\"", \""pass\"": \""password-example\""}""")]
    [InlineData(@"""{}""")]
    public async Task Put_CustomerOriginStrategy_400_IfCredentialsInvalid(string credentialsJson)
    {
        // Arrange
        const int customerId = 101;
        
        var strategyChangesJson = $@"{{
            ""strategy"": ""basic-http-authentication"",
            ""credentials"": {credentialsJson},
            ""regex"": ""http[s]?://(.*).example2.com"",
            ""order"": ""2"",
            ""optimised"": ""false""
        }}";
        var strategy = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "http[s]?://(.*).example.com",
            Strategy = OriginStrategyType.BasicHttp,
            Order = 1
        };
        var path = $"customers/{customerId}/originStrategies/{strategy.Id}";
          
        await dlcsContext.CustomerOriginStrategies.AddAsync(strategy);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var content = new StringContent(strategyChangesJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Put_CustomerOriginStrategy_400_IfCredentialsUsedWithoutBasicHttpAuth()
    {
        // Arrange
        const int customerId = 102;
        const string strategyChangesJson = @"{
            ""strategy"": ""s3-ambient"",
            ""credentials"": ""{\""user\"": \""user-example\"", \""password\"": \""password-example\""}"",
            ""regex"": ""http[s]?://(.*).example.com"",
            ""optimised"": ""false"",
            ""order"": ""2""
        }";
        
        var strategy = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "http[s]?://(.*).example.com",
            Strategy = OriginStrategyType.BasicHttp,
            Order = 1
        };
        var path = $"customers/{customerId}/originStrategies/{strategy.Id}";
          
        await dlcsContext.CustomerOriginStrategies.AddAsync(strategy);
        await dlcsContext.SaveChangesAsync();
     
        // Act
        var content = new StringContent(strategyChangesJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Put_CustomerOriginStrategy_400_IfOnlyCredentialsProvided()
    {
        // Arrange
        const int customerId = 103;
        const string strategyChangesJson = @"{
            ""credentials"": ""{\""user\"": \""user-example\"", \""password\"": \""password-example\""}"",
        }";
        
        var strategy = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "http[s]?://(.*).example.com",
            Strategy = OriginStrategyType.BasicHttp,
            Order = 1
        };
        var path = $"customers/{customerId}/originStrategies/{strategy.Id}";
        
        await dlcsContext.CustomerOriginStrategies.AddAsync(strategy);
        await dlcsContext.SaveChangesAsync();
        
        // Act
        var content = new StringContent(strategyChangesJson, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(customerId).PutAsync(path, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Delete_CustomerOriginStrategy_204()
    {
        // Arrange
        const int customerId = 104;
        
        var strategy = new CustomerOriginStrategy()
        {
            Id = Guid.NewGuid().ToString(),
            Customer = customerId,
            Regex = "http[s]?://(.*).example.com",
            Strategy = OriginStrategyType.S3Ambient,
            Optimised = true,
            Order = 1
        };
        var path = $"customers/{customerId}/originStrategies/{strategy.Id}";

        await dlcsContext.CustomerOriginStrategies.AddAsync(strategy);
        await dlcsContext.SaveChangesAsync();

        // Act
        var response = await httpClient.AsCustomer(customerId).DeleteAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var strategyExists = dlcsContext.CustomerOriginStrategies.Any(s => s.Id == strategy.Id);
        strategyExists.Should().BeFalse();
    }
    
    [Fact]
    public async Task Delete_CustomerOriginStrategy_404_IfNotFound()
    {
        // Arrange
        const int customerId = 105;
        
        var path = $"customers/{customerId}/originStrategies/{Guid.Empty}";
        
        // Act
        var response = await httpClient.AsCustomer(customerId).DeleteAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}