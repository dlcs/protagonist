using System;
using System.Collections.Generic;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model.Customers;
using DLCS.Repository.Customers;
using LazyCache.Mocks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Integration;
using Test.Helpers.Settings;

namespace DLCS.Repository.Tests.Customers;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class CustomerOriginStrategyRepositoryTests
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly DlcsContext dbContext;
    private readonly CustomerOriginStrategyRepository sut;
    
    public CustomerOriginStrategyRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        this.dbFixture = dbFixture;
        dbContext = dbFixture.DbContext;
        sut = GetSut();

        dbFixture.CleanUp();
    }

    private CustomerOriginStrategyRepository GetSut(Dictionary<string, string>? additionalSettings = null)
    {
        var settings = new Dictionary<string, string> { ["S3OriginRegex"] = "http\\:\\/\\/s3-/.*" };
        foreach (var (key, value) in additionalSettings ?? new Dictionary<string, string>())
        {
            settings[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new CustomerOriginStrategyRepository(new MockCachingService(), dbFixture.DbContext, configuration,
            OptionsHelpers.GetOptionsMonitor(new CacheSettings()), new NullLogger<CustomerOriginStrategyRepository>());
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
            new CustomerOriginStrategyRepository(null, null, configuration,
                OptionsHelpers.GetOptionsMonitor(new CacheSettings()), null);
        
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

    [Fact]
    public async Task GetCustomerOriginStrategy_DoesNotBacktrack_ForCatastrophicRegex()
    {
        // Arrange
        // "^(a+)+$" takes exponential time to reject non-matching input on a backtracking engine
        await dbContext.CustomerOriginStrategies.AddAsync(new CustomerOriginStrategy
        {
            Customer = 5, Id = "catastrophic", Regex = "^(a+)+$", Strategy = OriginStrategyType.S3Ambient, Order = 1
        });
        await dbContext.SaveChangesAsync();

        // Act
        var result = await sut.GetCustomerOriginStrategy(new AssetId(5, 1, "whatever"), new string('a', 60) + "!");

        // Assert
        result.Id.Should().Be("_default_");
    }

    [Fact]
    public async Task GetCustomerOriginStrategy_Throws_IfRegexInvalid()
    {
        // Arrange
        // Validation prevents this via the API but rows can predate it, or be written directly to the database
        await dbContext.CustomerOriginStrategies.AddAsync(new CustomerOriginStrategy
        {
            Customer = 5, Id = "invalid", Regex = "http[s?://", Strategy = OriginStrategyType.S3Ambient, Order = 1
        });
        await dbContext.SaveChangesAsync();

        // Act
        Func<Task> action = () => sut.GetCustomerOriginStrategy(new AssetId(5, 1, "whatever"), "https://matching.io/x");

        // Assert
        (await action.Should().ThrowAsync<OriginStrategyRegexException>())
            .Which.StrategyId.Should().Be("invalid");
    }

    [Fact]
    public async Task GetCustomerOriginStrategy_Throws_IfMatchTimesOut()
    {
        // Arrange
        // NOTE(DG): With NonBacktracking disabled the timeout is the only protection against a catastrophic regex.
        // Timing out must not fall through to a lower priority strategy, which could fetch with the wrong credentials
        await dbContext.CustomerOriginStrategies.AddRangeAsync(
            new CustomerOriginStrategy
            {
                Customer = 5, Id = "catastrophic", Regex = "^(a+)+$", Strategy = OriginStrategyType.S3Ambient,
                Order = 1
            },
            new CustomerOriginStrategy
            {
                Customer = 5, Id = "lower_priority", Regex = "(.*)", Strategy = OriginStrategyType.BasicHttp, Order = 2
            });
        await dbContext.SaveChangesAsync();

        var noNonBacktracking = GetSut(new Dictionary<string, string>
        {
            ["OriginStrategyRegex:UseNonBacktracking"] = "false",
            ["OriginStrategyRegex:MatchTimeout"] = "00:00:00.050"
        });

        // Act
        Func<Task> action = () =>
            noNonBacktracking.GetCustomerOriginStrategy(new AssetId(5, 1, "whatever"), new string('a', 60) + "!");

        // Assert
        (await action.Should().ThrowAsync<OriginStrategyRegexException>())
            .Which.StrategyId.Should().Be("catastrophic");
    }

    [Fact]
    public async Task GetCustomerOriginStrategy_StillMatches_ForPatternRequiringBacktracking()
    {
        // Arrange
        // Excluding file extensions needs a negative lookbehind, which NonBacktracking can't evaluate. These
        // strategies predate validation so must keep working, via the backtracking + timeout fallback
        var expected = new CustomerOriginStrategy
        {
            Customer = 5, Id = "lookbehind", Regex = "https://example.com/bucket/.*(?<!\\.tif|\\.jpg)$",
            Strategy = OriginStrategyType.S3Ambient, Order = 1
        };
        await dbContext.CustomerOriginStrategies.AddAsync(expected);
        await dbContext.SaveChangesAsync();

        // Act
        var matches = await sut.GetCustomerOriginStrategy(new AssetId(5, 1, "whatever"),
            "https://example.com/bucket/b1234.jp2");
        var excluded = await sut.GetCustomerOriginStrategy(new AssetId(5, 1, "whatever"),
            "https://example.com/bucket/b1234.tif");

        // Assert
        matches.Should().BeEquivalentTo(expected);
        excluded.Id.Should().Be("_default_");
    }
}
