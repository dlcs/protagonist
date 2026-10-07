using DLCS.Core.Types;

namespace DLCS.Core.Tests.Types;

public class DeliverableIdTests
{
    [Fact]
    public void ToString_CorrectFormat_Asset()
    {
        var deliverableId = new DeliverableId(new AssetId(19, 4, "my-first-image"));

        deliverableId.ToString().Should().Be("19/4/my-first-image");
        deliverableId.IsAdjunct.Should().BeFalse();
    }

    [Fact]
    public void ToString_CorrectFormat_Adjunct()
    {
        var deliverableId = new DeliverableId(new AssetId(19, 4, "my-first-image"), "mets.xml");

        deliverableId.ToString().Should().Be("19/4/my-first-image/mets.xml");
        deliverableId.IsAdjunct.Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_FromAssetId()
    {
        var assetId = new AssetId(19, 4, "my-first-image");

        DeliverableId deliverableId = assetId;

        deliverableId.AssetId.Should().Be(assetId);
        deliverableId.AdjunctId.Should().BeNull();
    }
}
