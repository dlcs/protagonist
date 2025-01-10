using System.Linq;
using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace DLCS.Model.Tests.Assets;

public class BatchXTests
{
    [Fact]
    public void AddBatchAsset_Adds_IfBatchAssetsNull()
    {
        var batch = new Batch { BatchAssets = null };
        var assetId = new AssetId(99, 100, "hi");
        
        batch.AddBatchAsset(assetId);

        var ba = batch.BatchAssets.Single();
        ba.Finished.Should().BeNull();
        ba.Status.Should().Be(BatchAssetStatus.Waiting);
    }
    
    [Theory]
    [InlineData(BatchAssetStatus.Waiting)]
    [InlineData(BatchAssetStatus.Error)]
    [InlineData(BatchAssetStatus.Completed)]
    public void AddBatchAsset_Adds_WithSpecifiedStatus(BatchAssetStatus status)
    {
        var batch = new Batch { BatchAssets = null };
        var assetId = new AssetId(99, 100, "hi");
        
        batch.AddBatchAsset(assetId, status);

        var ba = batch.BatchAssets.Single();
        ba.Finished.Should().BeNull();
        ba.Status.Should().Be(status);
    }
}