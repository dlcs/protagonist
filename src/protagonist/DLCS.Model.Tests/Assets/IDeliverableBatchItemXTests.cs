using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace DLCS.Model.Tests.Assets;

public class IDeliverableBatchItemXTests
{
    private static BatchAsset BuildBatchItem() => new()
    {
        BatchId = 1,
        AssetId = new AssetId(1, 1, "test-asset"),
        Status = BatchStatus.Waiting,
    };

    private static Adjunct BuildDeliverable(string error) => new()
    {
        Id = "test-adjunct",
        AssetId = new AssetId(1, 1, "test-asset"),
        MediaType = "image/jpeg",
        IIIFLink = IIIFLinkType.SeeAlso,
        Type = "Image",
        Error = error,
    };

    [Fact]
    public void FinishBatchItem_SetsStatusCompleted_WhenNoError()
    {
        var item = BuildBatchItem();
        var deliverable = BuildDeliverable(null);

        item.FinishBatchItem(deliverable);

        item.Status.Should().Be(BatchStatus.Completed);
        item.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FinishBatchItem_SetsStatusCompleted_WhenErrorNullOrEmpty(string error)
    {
        var item = BuildBatchItem();
        var deliverable = BuildDeliverable(error);

        item.FinishBatchItem(deliverable);

        item.Status.Should().Be(BatchStatus.Completed);
    }

    [Fact]
    public void FinishBatchItem_SetsStatusError_AndSetsErrorMessage_WhenErrorPresent()
    {
        var item = BuildBatchItem();
        var deliverable = BuildDeliverable("something went wrong");

        item.FinishBatchItem(deliverable);

        item.Status.Should().Be(BatchStatus.Error);
        item.Error.Should().Be("something went wrong");
    }

    [Fact]
    public void FinishBatchItem_SetsFinished_Regardless()
    {
        var item = BuildBatchItem();
        var deliverable = BuildDeliverable(null);

        item.FinishBatchItem(deliverable);

        item.Finished.Should().NotBeNull();
    }
}
