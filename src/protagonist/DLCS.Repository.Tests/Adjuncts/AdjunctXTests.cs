using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Types;
using DLCS.Repository.Adjuncts;
using Microsoft.EntityFrameworkCore;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Adjuncts;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class AdjunctXTests
{
    private readonly DlcsContext dbContext;
    private readonly AssetId firstAssetId = new(1, 1, "testAsset_1");
    private readonly AssetId secondAssetId = new(1, 1, "testAsset_2");
    
    public AdjunctXTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbFixture.CleanUp();
        
        dbContext.Images.AddTestAsset(firstAssetId)
            .WithTestAdjunct("someAdjunct_1")
            .WithTestAdjunct("someAdjunct_2");
        
        dbContext.Images.AddTestAsset(secondAssetId)
            .WithTestAdjunct("someAdjunct_1")
            .WithTestAdjunct("someAdjunct_2");

        dbContext.SaveChanges();
    }
    
    [Fact]
    public void FindAdjuncts_FindsSingleAdjunct()
    {
        // Arrange
        var adjunctsToFind = new Dictionary<AssetId, List<string>>
        {
            {firstAssetId, ["someAdjunct_1"]}
        };
        
        // Act
        var foundAdjuncts = dbContext.Adjuncts.FindAdjuncts(adjunctsToFind).ToList();

        // Assert
        foundAdjuncts.Should().HaveCount(1);
        foundAdjuncts.First().Id.Should().Be("someAdjunct_1");
        foundAdjuncts.First().AssetId.Should().Be(firstAssetId);
    }
    
    [Fact]
    public void FindAdjuncts_FindsMultipleAdjunctInSingleAsset()
    {
        // Arrange
        var adjunctsToFind = new Dictionary<AssetId, List<string>>
        {
            {firstAssetId, ["someAdjunct_1", "someAdjunct_2"]}
        };
        
        // Act
        var foundAdjuncts = dbContext.Adjuncts.FindAdjuncts(adjunctsToFind).ToList();

        // Assert
        foundAdjuncts.Should().HaveCount(2);
        foundAdjuncts.First().Id.Should().Be("someAdjunct_1");
        foundAdjuncts.First().AssetId.Should().Be(firstAssetId);
        foundAdjuncts.Last().Id.Should().Be("someAdjunct_2");
        foundAdjuncts.Last().AssetId.Should().Be(firstAssetId);
    }
    
    [Fact]
    public void FindAdjuncts_FindsSingleAdjunctInMultipleAsset()
    {
        // Arrange
        var adjunctsToFind = new Dictionary<AssetId, List<string>>
        {
            {firstAssetId, ["someAdjunct_1"]},
            {secondAssetId, ["someAdjunct_1"]}
        };
        
        // Act
        var foundAdjuncts = dbContext.Adjuncts.FindAdjuncts(adjunctsToFind).ToList();

        // Assert
        foundAdjuncts.Should().HaveCount(2);
        foundAdjuncts.First().Id.Should().Be("someAdjunct_1");
        foundAdjuncts.First().AssetId.Should().Be(firstAssetId);
        foundAdjuncts.Last().Id.Should().Be("someAdjunct_1");
        foundAdjuncts.Last().AssetId.Should().Be(secondAssetId);
    }
    
    [Fact]
    public async Task FindAdjuncts_FindsMultipleAdjunctInMultipleAsset()
    {
        // Arrange
        var adjunctsToFind = new Dictionary<AssetId, List<string>>
        {
            {firstAssetId, ["someAdjunct_1", "someAdjunct_2"]},
            {secondAssetId, ["someAdjunct_1", "someAdjunct_2"]}
        };
        
        // Act
        var foundAdjuncts = await dbContext.Adjuncts.FindAdjuncts(adjunctsToFind).ToListAsync();

        // Assert
        foundAdjuncts.Should().HaveCount(4);
        foundAdjuncts.First().Id.Should().Be("someAdjunct_1");
        foundAdjuncts.First().AssetId.Should().Be(firstAssetId);
        foundAdjuncts[1].Id.Should().Be("someAdjunct_1");
        foundAdjuncts[1].AssetId.Should().Be(secondAssetId);
        
        foundAdjuncts[2].Id.Should().Be("someAdjunct_2");
        foundAdjuncts[2].AssetId.Should().Be(firstAssetId);
        foundAdjuncts.Last().Id.Should().Be("someAdjunct_2");
        foundAdjuncts.Last().AssetId.Should().Be(secondAssetId);
    }
    
    [Fact]
    public void FindAdjuncts_FindsNoAdjunctsWhenNotFound()
    {
        // Arrange
        var adjunctsToFind = new Dictionary<AssetId, List<string>>
        {
            {new AssetId(1, 1, "notFound"), ["someAdjunct_1"]}
        };
        
        // Act
        var foundAdjuncts = dbContext.Adjuncts.FindAdjuncts(adjunctsToFind).ToList();

        // Assert
        foundAdjuncts.Should().HaveCount(0);
    }
    
    [Fact]
    public void FindAdjuncts_FindsNoAdjunctsWhenNothingToCheck()
    {
        // Arrange
        var adjunctsToFind = new Dictionary<AssetId, List<string>>();

        // Act
        var foundAdjuncts = dbContext.Adjuncts.FindAdjuncts(adjunctsToFind).ToList();

        // Assert
        foundAdjuncts.Should().HaveCount(0);
    }

    [Fact]
    public void FindAdjuncts_Lookup_FindsSingleAdjunct()
    {
        var adjunctsToFind = new[] { (firstAssetId, "someAdjunct_1") }
            .ToLookup(x => x.Item1, x => x.Item2);

        var foundAdjuncts = dbContext.Adjuncts.FindAdjuncts(adjunctsToFind).ToList();

        foundAdjuncts.Should().HaveCount(1);
        foundAdjuncts.First().Id.Should().Be("someAdjunct_1");
        foundAdjuncts.First().AssetId.Should().Be(firstAssetId);
    }

    [Fact]
    public void FindAdjuncts_Lookup_FindsMultipleAdjunctsInSingleAsset()
    {
        var adjunctsToFind = new[] { (firstAssetId, "someAdjunct_1"), (firstAssetId, "someAdjunct_2") }
            .ToLookup(x => x.Item1, x => x.Item2);

        var foundAdjuncts = dbContext.Adjuncts.FindAdjuncts(adjunctsToFind).ToList();

        foundAdjuncts.Should().HaveCount(2);
        foundAdjuncts.First().Id.Should().Be("someAdjunct_1");
        foundAdjuncts.First().AssetId.Should().Be(firstAssetId);
        foundAdjuncts.Last().Id.Should().Be("someAdjunct_2");
        foundAdjuncts.Last().AssetId.Should().Be(firstAssetId);
    }

    [Fact]
    public async Task FindAdjuncts_Lookup_FindsSingleAdjunctInMultipleAssets()
    {
        var adjunctsToFind = new[] { (firstAssetId, "someAdjunct_1"), (secondAssetId, "someAdjunct_1") }
            .ToLookup(x => x.Item1, x => x.Item2);

        var foundAdjuncts = await dbContext.Adjuncts.FindAdjuncts(adjunctsToFind).ToListAsync();

        foundAdjuncts.Should().HaveCount(2);
        foundAdjuncts.First().Id.Should().Be("someAdjunct_1");
        foundAdjuncts.First().AssetId.Should().Be(firstAssetId);
        foundAdjuncts.Last().Id.Should().Be("someAdjunct_1");
        foundAdjuncts.Last().AssetId.Should().Be(secondAssetId);
    }

    [Fact]
    public async Task FindAdjuncts_Lookup_FindsMultipleAdjunctsInMultipleAssets()
    {
        var adjunctsToFind = new[]
            {
                (firstAssetId, "someAdjunct_1"), (firstAssetId, "someAdjunct_2"),
                (secondAssetId, "someAdjunct_1"), (secondAssetId, "someAdjunct_2")
            }
            .ToLookup(x => x.Item1, x => x.Item2);

        var foundAdjuncts = await dbContext.Adjuncts.FindAdjuncts(adjunctsToFind).ToListAsync();

        foundAdjuncts.Should().HaveCount(4);
        foundAdjuncts.First().Id.Should().Be("someAdjunct_1");
        foundAdjuncts.First().AssetId.Should().Be(firstAssetId);
        foundAdjuncts[1].Id.Should().Be("someAdjunct_1");
        foundAdjuncts[1].AssetId.Should().Be(secondAssetId);
        foundAdjuncts[2].Id.Should().Be("someAdjunct_2");
        foundAdjuncts[2].AssetId.Should().Be(firstAssetId);
        foundAdjuncts.Last().Id.Should().Be("someAdjunct_2");
        foundAdjuncts.Last().AssetId.Should().Be(secondAssetId);
    }

    [Fact]
    public async Task FindAdjuncts_Lookup_FindsNoAdjunctsWhenNotFound()
    {
        var adjunctsToFind = new[] { (new AssetId(1, 1, "notFound"), "someAdjunct_1") }
            .ToLookup(x => x.Item1, x => x.Item2);

        var foundAdjuncts = await dbContext.Adjuncts.FindAdjuncts(adjunctsToFind).ToListAsync();

        foundAdjuncts.Should().HaveCount(0);
    }

    [Fact]
    public async Task FindAdjuncts_Lookup_FindsNoAdjunctsWhenNothingToCheck()
    {
        var adjunctsToFind = Enumerable.Empty<(AssetId, string)>().ToLookup(x => x.Item1, x => x.Item2);

        var foundAdjuncts = await dbContext.Adjuncts.FindAdjuncts(adjunctsToFind).ToListAsync();

        foundAdjuncts.Should().HaveCount(0);
    }
}
