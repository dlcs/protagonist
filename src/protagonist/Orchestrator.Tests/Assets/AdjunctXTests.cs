using DLCS.Core.Types;
using DLCS.Model.Assets;
using Orchestrator.Assets;

namespace Orchestrator.Tests.Assets;

public class AdjunctXTests
{
    [Fact]
    public void GetDeliverableRoles_ReturnsParentAssetRoles()
    {
        var adjunct = CreateAdjunct();
        adjunct.Asset = new Asset { Roles = ["clickthrough", "logged-in"] };

        adjunct.GetDeliverableRoles().Should().BeEquivalentTo("clickthrough", "logged-in");
    }

    [Fact]
    public void GetDeliverableRoles_ReturnsEmpty_IfParentAssetHasNoRoles()
    {
        var adjunct = CreateAdjunct();
        adjunct.Asset = new Asset { Roles = null };

        adjunct.GetDeliverableRoles().Should().BeEmpty();
    }

    [Fact]
    public void GetDeliverableRoles_ReturnsEmpty_IfNoParentAsset()
    {
        var adjunct = CreateAdjunct();

        adjunct.GetDeliverableRoles().Should().BeEmpty();
    }

    private static Adjunct CreateAdjunct() => new()
    {
        Id = "adjunct",
        AssetId = new AssetId(1, 2, "asset"),
        MediaType = "text/plain",
        IIIFLink = IIIFLinkType.SeeAlso,
        Type = "a_type"
    };
}
