using System.Linq;
using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using DLCS.Repository.Assets;
using DLCS.Repository.Caching;
using FluentAssertions;
using LazyCache.Mocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;
using Xunit;

namespace DLCS.Repository.Tests.Assets;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class NamedQueryRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly NamedQueryRepository sut;

    public NamedQueryRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        sut = new NamedQueryRepository(dbFixture.DbContext, new MockCachingService(),
            Options.Create(new CacheSettings()));

        dbFixture.CleanUp();
        dbContext.NamedQueries.AddTestNamedQuery("global-and-local", 98, global: true);
        dbContext.NamedQueries.AddTestNamedQuery("global-and-local", 99, global: false);
        dbContext.NamedQueries.AddTestNamedQuery("different-customer", 98, global: false);
        dbContext.NamedQueries.AddTestNamedQuery("customer", 99, global: false);

        dbContext.Images.AddTestAsset("99/1/1", ref1: "foobar");
        dbContext.Images.AddTestAsset("99/1/2", ref2: "foobar");
        dbContext.Images.AddTestAsset("99/1/3", ref3: "foobar");
        dbContext.Images.AddTestAsset("99/1/4", num1: 1);
        dbContext.Images.AddTestAsset("99/1/5", num2: 1);
        dbContext.Images.AddTestAsset("99/1/6", num3: 1);
        dbContext.Images.AddTestAsset("99/1/7", space: 2);
        dbContext.Images.AddTestAsset("99/1/8", ref1: "foo", ref2: "bar", ref3: "baz", num1: 5, num2: 10, num3: 20);
        dbContext.SaveChanges();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetByName_Null_IfNoMatching(bool includeGlobal)
    {
        // Act
        var namedQuery = await sut.GetByName(99, "non-existant", includeGlobal);

        // Assert
        namedQuery.Should().BeNull();
    }

    [Fact]
    public async Task GetByName_Null_IfDifferentCustomer()
    {
        // Act
        var namedQuery = await sut.GetByName(99, "different-customer");

        // Assert
        namedQuery.Should().BeNull();
    }

    [Fact]
    public async Task GetByName_ReturnsExpected_NonGlobal()
    {
        // Act
        var namedQuery = await sut.GetByName(99, "customer");

        // Assert
        namedQuery.Name.Should().Be("customer");
        namedQuery.Customer.Should().Be(99);
    }

    [Fact]
    public async Task GetByName_ReturnsLocalFirst_IfMatchingGlobal()
    {
        // Act
        var namedQuery = await sut.GetByName(99, "global-and-local");

        // Assert
        namedQuery.Name.Should().Be("global-and-local");
        namedQuery.Customer.Should().Be(99);
    }

    [Fact]
    public async Task GetByName_ReturnsGlobal_IfIfNoLocal()
    {
        // Act
        var namedQuery = await sut.GetByName(3, "global-and-local");

        // Assert
        namedQuery.Name.Should().Be("global-and-local");
        namedQuery.Customer.Should().Be(98);
    }

    [Fact]
    public async Task GetNamedQueryResults_Empty_IfNoMatches()
    {
        // Arrange
        var query = new ParsedNamedQuery(new CustomerPathElement(1, "notfound"));

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNamedQueryResults_ReturnsAllForCustomer_IfNoOtherCriteria()
    {
        // Arrange
        var query = new ParsedNamedQuery(new CustomerPathElement(99, "test"));

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Count().Should().Be(8);
    }

    [Fact]
    public async Task GetNamedQueryResults_FilterByString1()
    {
        // Arrange
        var query = new ParsedNamedQuery(new CustomerPathElement(99, "test"))
        {
            String1 = "foobar"
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Single().Id.Should().Be("99/1/1");
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByString2()
    {
        // Arrange
        var query = new ParsedNamedQuery(new CustomerPathElement(99, "test"))
        {
            String2 = "foobar"
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Single().Id.Should().Be("99/1/2");
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByString3()
    {
        // Arrange
        var query = new ParsedNamedQuery(new CustomerPathElement(99, "test"))
        {
            String3 = "foobar"
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Single().Id.Should().Be("99/1/3");
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByNumber1()
    {
        // Arrange
        var query = new ParsedNamedQuery(new CustomerPathElement(99, "test"))
        {
            Number1 = 1
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Single().Id.Should().Be("99/1/4");
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByNumber2()
    {
        // Arrange
        var query = new ParsedNamedQuery(new CustomerPathElement(99, "test"))
        {
            Number2 = 1
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Single().Id.Should().Be("99/1/5");
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByNumber3()
    {
        // Arrange
        var query = new ParsedNamedQuery(new CustomerPathElement(99, "test"))
        {
            Number3 = 1
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Single().Id.Should().Be("99/1/6");
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterBySpace()
    {
        // Arrange
        var query = new ParsedNamedQuery(new CustomerPathElement(99, "test"))
        {
            Space = 1
        };
        
        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Count().Should().Be(7);
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterBySpaceName()
    {
        // Arrange
        var query = new ParsedNamedQuery(new CustomerPathElement(99, "test"))
        {
            SpaceName = "space-1"
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Count().Should().Be(7);
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterBySpaceAndSpaceName_SpaceTakesPriority()
    {
        // Arrange
        var query = new ParsedNamedQuery(new CustomerPathElement(99, "test"))
        {
            Space = 1, SpaceName = "unknown-space"
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Count().Should().Be(7);
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByMultipleCriteria()
    {
        // Arrange
        var query = new ParsedNamedQuery(new CustomerPathElement(99, "test"))
        {
            Space = 1, SpaceName = "unknown-space", String1 = "foo", String2 = "bar", String3 = "baz", Number1 = 5,
            Number2 = 10, Number3 = 20
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Single().Id.Should().Be("99/1/8");
    }
}