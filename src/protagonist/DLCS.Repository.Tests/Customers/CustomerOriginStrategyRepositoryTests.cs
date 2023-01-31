using System;
using System.Collections.Generic;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model.Customers;
using DLCS.Repository.Customers;
using LazyCache.Mocks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Customers;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class CustomerOriginStrategyRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly CustomerOriginStrategyRepository sut;
    
    public CustomerOriginStrategyRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new KeyValuePair<string, string>[] { new("S3OriginRegex", "http\\:\\/\\/s3-/.*") })
            .Build();
        
        sut = new CustomerOriginStrategyRepository(new MockCachingService(), dbFixture.DbContext, configuration,
            Options.Create(new CacheSettings()), new NullLogger<CustomerOriginStrategyRepository>());
        
        dbFixture.CleanUp();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("no_val")]
    public void Ctor_Throws_IfS3OriginRegex_MissingOrNullOrWhitespace(string s3Origin)
    {
        // Arrange
        var sampleDictionary = new Dictionary<string, string>();
        if (s3Origin != "no_val")
        {
            sampleDictionary.Add("S3OriginRegex", s3Origin);
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(sampleDictionary).Build();

        // Act
        Action action = () =>
            new CustomerOriginStrategyRepository(null, null, configuration, Options.Create(new CacheSettings()),
                null);
        
        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'appsetting:S3OriginRegex')");
    }

    [Fact]
    public async Task GetCustomerOriginStrategies_ReturnsDbStrategiesPlusPortalOrigin()
    {
        // Arrange
        var originStrategies = new List<CustomerOriginStrategy>
        {
            new() { Customer = 5, Id = "default", Regex = "whatev", Strategy = OriginStrategyType.Default },
            new() { Customer = 5, Id = "basic", Regex = "whatev", Strategy = OriginStrategyType.BasicHttp },
            new() { Customer = 5, Id = "s3", Regex = "whatev", Strategy = OriginStrategyType.S3Ambient }
        };
        await dbContext.CustomerOriginStrategies.AddRangeAsync(originStrategies);
        await dbContext.SaveChangesAsync();

        // Act
        var results = await sut.GetCustomerOriginStrategies(5);
        
        // Assert
        results.Should().HaveCount(4);
        results.Should().Contain(cos => cos.Id == "default" && cos.Strategy == OriginStrategyType.Default);
        results.Should().Contain(cos => cos.Id == "basic" && cos.Strategy == OriginStrategyType.BasicHttp);
        results.Should().Contain(cos => cos.Id == "s3" && cos.Strategy == OriginStrategyType.S3Ambient);
        results.Should()
            .Contain(cos => cos.Id == "_default_portal_" && cos.Strategy == OriginStrategyType.S3Ambient);
    }

    [Fact]
    public async Task GetCustomerOriginStrategy_ReturnsStrategyForOrigin()
    {
        // Arrange
        var expected = new CustomerOriginStrategy
        {
            Customer = 5, Id = "matching", Regex = "http[s]?://matching.io/(.*)",
            Strategy = OriginStrategyType.S3Ambient, Order = 10
        };
        var originStrategies = new List<CustomerOriginStrategy>
        {
            new()
            {
                Customer = 5, Id = "not_matching", Regex = "http[s]?://(.*).test.example",
                Strategy = OriginStrategyType.S3Ambient, Order = 5
            },
            expected,
            new()
            {
                Customer = 5, Id = "matching_but_lower_priority", Regex = "https://matching.io/(.*)",
                Strategy = OriginStrategyType.S3Ambient, Order = 15
            }
        };
        await dbContext.CustomerOriginStrategies.AddRangeAsync(originStrategies);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await sut.GetCustomerOriginStrategy(new AssetId(5, 1, "whatever"), "https://matching.io/bla");
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task GetCustomerOriginStrategy_ReturnsDefaultStrategy_IfNoMatchFound()
    {
        // Arrange
        var expected = new CustomerOriginStrategy { Id = "_default_", Strategy = OriginStrategyType.Default };
        
        // Act
        var result = await sut.GetCustomerOriginStrategy(new AssetId(5, 1, "whatever"),
            nameof(GetCustomerOriginStrategy_ReturnsDefaultStrategy_IfNoMatchFound));
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }
}