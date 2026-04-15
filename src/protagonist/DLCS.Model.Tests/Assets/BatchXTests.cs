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
        ba.Status.Should().Be(BatchStatus.Waiting);
    }
    
    [Theory]
    [InlineData(BatchStatus.Waiting)]
    [InlineData(BatchStatus.Error)]
    [InlineData(BatchStatus.Completed)]
    public void AddBatchAsset_Adds_WithSpecifiedStatus(BatchStatus status)
    {
        var batch = new Batch { BatchAssets = null };
        var assetId = new AssetId(99, 100, "hi");
        
        batch.AddBatchAsset(assetId, status);

        var ba = batch.BatchAssets.Single();
        ba.Finished.Should().BeNull();
        ba.Status.Should().Be(status);
    }
}