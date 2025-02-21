using System.Collections.Generic;
using System.Linq;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using DLCS.Repository.Assets;
using Microsoft.EntityFrameworkCore;
using Test.Helpers.Data;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Assets;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class AssetQueryXTests
{
    private readonly DlcsContext dbContext;
    
    public AssetQueryXTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task IncludeDeliveryChannelsWithPolicy_ReturnsDeliveryChannels_ByOrderOfChannel()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.ImageDeliveryChannels.AddRangeAsync(
            new()
            {
                ImageId = assetId, Channel = "gamma",
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
            },
            new()
            {
                ImageId = assetId, Channel = "alpha",
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
            },
            new()
            {
                ImageId = assetId, Channel = "beta",
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
            });
        await dbContext.Images.AddTestAsset(assetId);
        await dbContext.SaveChangesAsync();
        
        // Act
        var result = await dbContext.Images
            .Where(i => i.Id == assetId)
            .IncludeDeliveryChannelsWithPolicy()
            .ToListAsync();

        // Assert
        result.Single().ImageDeliveryChannels.Should().BeInAscendingOrder(idc => idc.Channel);
    }
}