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

    [Theory]
    [InlineData(null)]
    [InlineData("mets.xml")]
    public void Equals_True_IfSameAssetAndAdjunct(string? adjunctId)
    {
        var first = new DeliverableId(new AssetId(19, 4, "my-first-image"), adjunctId);
        var second = new DeliverableId(new AssetId(19, 4, "my-first-image"), adjunctId);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Theory]
    [InlineData("my-first-image", null, "my-first-image", "mets.xml")]
    [InlineData("my-first-image", "mets.xml", "my-first-image", "alto.xml")]
    [InlineData("my-first-image", "mets.xml", "other-image", "mets.xml")]
    public void Equals_False_IfDifferent(string asset1, string? adjunct1, string asset2, string? adjunct2)
    {
        var first = new DeliverableId(new AssetId(19, 4, asset1), adjunct1);
        var second = new DeliverableId(new AssetId(19, 4, asset2), adjunct2);

        first.Should().NotBe(second);
    }
}
