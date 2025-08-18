using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.Assets;
using LazyCache.Mocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.NamedQueries;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class NamedQueryRepositoryTests
{
    private readonly NamedQueryRepository sut;

    public NamedQueryRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        var dbContext = dbFixture.DbContext;
        sut = new NamedQueryRepository(dbFixture.DbContext, new MockCachingService(),
            Options.Create(new CacheSettings()));

        dbFixture.CleanUp();
        dbContext.NamedQueries.AddTestNamedQuery("global-and-local", 98, global: true);
        dbContext.NamedQueries.AddTestNamedQuery("global-and-local", 99, global: false);
        dbContext.NamedQueries.AddTestNamedQuery("different-customer", 98, global: false);
        dbContext.NamedQueries.AddTestNamedQuery("customer", 99, global: false);

        dbContext.Images.AddTestAsset(AssetId.FromString("99/1/1"), ref1: "foobar");
        dbContext.Images.AddTestAsset(AssetId.FromString("99/1/2"), ref2: "foobar");
        dbContext.Images.AddTestAsset(AssetId.FromString("99/1/3"), ref3: "foobar");
        dbContext.Images.AddTestAsset(AssetId.FromString("99/1/4"), num1: 1);
        dbContext.Images.AddTestAsset(AssetId.FromString("99/1/5"), num2: 1);
        dbContext.Images.AddTestAsset(AssetId.FromString("99/1/6"), num3: 1);
        dbContext.Images.AddTestAsset(AssetId.FromString("99/1/7"), space: 2);
        dbContext.Images.AddTestAsset(AssetId.FromString("99/1/8"), ref1: "foo", ref2: "bar", ref3: "baz", num1: 5,
            num2: 10, num3: 20);
        
        // Batch records - first with 3 and second with 2. 1 asset is in both
        var batchedAsset1 = AssetId.FromString("99/2/1");
        var batchedAsset2 = AssetId.FromString("99/2/2");
        var batchedAsset3 = AssetId.FromString("99/2/3");
        var batchedAsset4 = AssetId.FromString("99/2/4");
        const int batchId1 = 101010;
        const int batchId2 = 101011;
        dbContext.Images.AddTestAsset(batchedAsset1, space: 2);
        dbContext.Images.AddTestAsset(batchedAsset2, space: 2, num3: 1);
        dbContext.Images.AddTestAsset(batchedAsset3, space: 2, num3: 1);
        dbContext.Images.AddTestAsset(batchedAsset4, space: 2, num3: 1);
        var batch = dbContext.Batches.AddTestBatch(batchId1).Result;
        batch.Entity
            .AddBatchAsset(batchedAsset1)
            .AddBatchAsset(batchedAsset2, BatchAssetStatus.Completed)
            .AddBatchAsset(batchedAsset3, BatchAssetStatus.Error);
        
        // Images with manifests - first with 2 and second with 2. 1 manifest is in both
        var manifestAsset1 = AssetId.FromString("99/3/1");
        var manifestAsset2 = AssetId.FromString("99/3/2");
        const string manifestId1 = "foo";
        const string manifestId2 = "bar";
        const string manifestId3 = "baz";
        dbContext.Images.AddTestAsset(manifestAsset1, space: 3, manifests: [manifestId1, manifestId2]);
        dbContext.Images.AddTestAsset(manifestAsset2, space: 3, num3: 1, manifests: [manifestId2, manifestId3]);
        
        var batch2 = dbContext.Batches.AddTestBatch(batchId2).Result;
        batch2.Entity.AddBatchAsset(batchedAsset1).AddBatchAsset(batchedAsset4);
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
        var query = new ParsedNamedQuery(1);

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNamedQueryResults_ReturnsAllForCustomer_IfNoOtherCriteria()
    {
        // Arrange
        var query = new ParsedNamedQuery(99);

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().HaveCount(14);
    }

    [Fact]
    public async Task GetNamedQueryResults_FilterByString1()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            String1 = "foobar"
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().ContainSingle(r => r.Id == AssetId.FromString("99/1/1"));
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByString2()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            String2 = "foobar"
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().ContainSingle(r => r.Id == AssetId.FromString("99/1/2"));
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByString3()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            String3 = "foobar"
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().ContainSingle(r => r.Id == AssetId.FromString("99/1/3"));
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByNumber1()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            Number1 = 1
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().ContainSingle(r => r.Id == AssetId.FromString("99/1/4"));
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByNumber2()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            Number2 = 1
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().ContainSingle(r => r.Id == AssetId.FromString("99/1/5"));
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByNumber3()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            Number3 = 1
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().ContainSingle(r => r.Id == AssetId.FromString("99/1/6"));
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterBySpace()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            Space = 1
        };
        
        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().HaveCount(7);
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterBySpaceName()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            SpaceName = "space-1"
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().HaveCount(7);
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterBySpaceAndSpaceName_SpaceTakesPriority()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            Space = 1, SpaceName = "unknown-space"
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().HaveCount(7);
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByMultipleCriteria()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            Space = 1, SpaceName = "unknown-space", String1 = "foo", String2 = "bar", String3 = "baz", Number1 = 5,
            Number2 = 10, Number3 = 20
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().ContainSingle(r => r.Id == AssetId.FromString("99/1/8"));
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterBySingleBatch()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            Batches = [101010]
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().HaveCount(3);
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByMultipleBatch()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            Batches = [101010, 101011]
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().HaveCount(4);
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByMultipleBatch_AndOtherCriteria()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            Batches = [101010, 101011], Number3 = 1 
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().HaveCount(3);
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterBySingleManifest()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            Manifests = ["bar"]
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().HaveCount(2, "'bar' is used by 2 manifests");
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByMultipleManifest()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            Manifests = ["foo", "baz"]
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().HaveCount(2, "'foo' is used by one manifest and 'baz' another");
    }
    
    [Fact]
    public async Task GetNamedQueryResults_FilterByMultipleManifest_AndOtherCriteria()
    {
        // Arrange
        var query = new ParsedNamedQuery(99)
        {
            Manifests = ["bar"], Number3 = 1 
        };

        // Act
        var result = await sut.GetNamedQueryResults(query).ToListAsync();

        // Assert
        result.Should().HaveCount(1, "'bar' is used by 2 manifests but only 1 has number3=1");
    }
}
