using System.Collections.Generic;
using System.Linq;
using DLCS.Model.Assets.Metadata;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Infrastructure;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Data;
using Test.Helpers.Integration;

namespace Orchestrator.Tests.Infrastructure;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class AssetQueryableXTests
{
    private readonly DlcsContext dbContext;

    public AssetQueryableXTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        
        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task IncludeRelationsForProjections_HandlesNoRelatedData()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();
        
        var asset = await dbContext.Images.Where(i => i.Id == assetId).IncludeRelationsForProjections().SingleAsync();

        asset.Should().NotBeNull("Asset returned despite having no related data");
        asset.AssetApplicationMetadata.Should().BeNullOrEmpty();
        asset.ImageDeliveryChannels.Should().BeNullOrEmpty();
        asset.Adjuncts.Should().BeNullOrEmpty();
    }
    
    [Fact]
    public async Task IncludeRelationsForProjections_ReturnsRelatedThumbs()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId).WithTestThumbnailMetadata().WithTestDeliveryChannel("iiif-img");
        await dbContext.SaveChangesAsync();
        
        var asset = await dbContext.Images.Where(i => i.Id == assetId).IncludeRelationsForProjections().SingleAsync();

        asset.Should().NotBeNull();
        asset.AssetApplicationMetadata.Should().HaveCount(1)
            .And.Subject.Single().MetadataType.Should().Be("ThumbSizes");
        asset.ImageDeliveryChannels.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task IncludeRelationsForProjections_ReturnsRelatedTranscodes()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var fakeTranscodes = new List<AVTranscode>
        {
            new() { Duration = 10, Width = 20, Height = 30 }
        };
        (await dbContext.Images.AddTestAsset(assetId)).Entity.WithTestTranscodeMetadata(fakeTranscodes);
        await dbContext.SaveChangesAsync();
        
        var asset = await dbContext.Images.Where(i => i.Id == assetId).IncludeRelationsForProjections().SingleAsync();

        asset.Should().NotBeNull();
        asset.AssetApplicationMetadata.Should().HaveCount(1)
            .And.Subject.Single().MetadataType.Should().Be("AVTranscodes");
    }

    [Fact]
    public async Task IncludeRelationsForProjections_ReturnsThumbsAndTranscodes()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var fakeTranscodes = new List<AVTranscode>
        {
            new() { Duration = 10, Width = 20, Height = 30 }
        };
        var entity = await dbContext.Images.AddTestAsset(assetId).WithTestThumbnailMetadata().WithTestDeliveryChannel("iiif-img");
        entity.Entity.WithTestTranscodeMetadata(fakeTranscodes);
        await dbContext.SaveChangesAsync();
        
        var asset = await dbContext.Images.Where(i => i.Id == assetId)
            .IncludeRelationsForProjections()
            .SingleAsync();

        asset.Should().NotBeNull();
        asset.AssetApplicationMetadata.Should().HaveCount(2);
        asset.AssetApplicationMetadata.Should().ContainSingle(m => m.MetadataType == "ThumbSizes");
        asset.AssetApplicationMetadata.Should().ContainSingle(m => m.MetadataType == "AVTranscodes");
        asset.ImageDeliveryChannels.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task IncludeRelationsForProjections_ReturnsAdjuncts()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        const string middleAlphaAdjunctId = "ekki mukk";
        const string lowestAlphaAdjunctId = "kebab";
        const string highestAlphaAdjunctId = "zombie";
        await dbContext.Images
            .AddTestAsset(assetId)
            .WithTestThumbnailMetadata()
            .WithTestDeliveryChannel("iiif-img")
            .WithTestAdjunct(highestAlphaAdjunctId)
            .WithTestAdjunct(lowestAlphaAdjunctId)
            .WithTestAdjunct(middleAlphaAdjunctId);
        await dbContext.SaveChangesAsync();
        
        var asset = await dbContext.Images.Where(i => i.Id == assetId).IncludeRelationsForProjections().SingleAsync();

        asset.Should().NotBeNull();
        asset.Adjuncts.Should().HaveCount(3);
        asset.Adjuncts.Should().BeInAscendingOrder(a => a.Id);
    }
}

