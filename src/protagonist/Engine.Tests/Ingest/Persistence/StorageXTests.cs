using DLCS.Core.Types;
using Engine.Ingest.Persistence;
using Engine.Settings;
using Test.Helpers.Data;

namespace Engine.Tests.Ingest.Persistence;

public class StorageXTests
{
    [Fact]
    public void GetDiskSafeAssetId_DoesNotChangeId_IfAlreadySafe()
    {
        var assetId = AssetIdGenerator.GetAssetId();

        var actual = assetId.GetDiskSafeAssetId(new ImageIngestSettings());

        actual.Should().Be(assetId.Asset, "'Asset' only contains valid chars so is unchanged");
    }
    
    [Fact]
    public void GetDiskSafeAssetId_ReplacesBrackets_DefaultReplacement()
    {
        const string incomingAsset = "((hello))__foo";
        const string expectedAsset = "__hello____foo";

        var assetId = new AssetId(1, 2, incomingAsset);

        var actual = assetId.GetDiskSafeAssetId(new ImageIngestSettings());

        actual.Should().Be(expectedAsset, "( and ) are replaced with underscore");
    }
    
    [Fact]
    public void GetDiskSafeAssetId_ReplacesBrackets_CustomReplacement()
    {
        const string incomingAsset = "((hello))__foo";
        const string expectedAsset = "oohellocc__foo";

        var assetId = new AssetId(1, 2, incomingAsset);

        var actual = assetId.GetDiskSafeAssetId(new ImageIngestSettings
        {
            OpenBracketReplacement = "o", CloseBracketReplacement = "c"
        });

        actual.Should().Be(expectedAsset, "( and ) are replaced with custom characters");
    }
}
