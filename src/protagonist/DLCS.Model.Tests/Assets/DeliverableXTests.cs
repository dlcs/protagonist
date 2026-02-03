using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace DLCS.Model.Tests.Assets;

public class DeliverableXTests
{
    [Fact]
    public void SetFieldsForIngestion_ClearsFields_Asset()
    {
        // Arrange
        var asset = new Asset { Error = "I am an error", Ingesting = false };
        var expected = new Asset { Error = string.Empty, Ingesting = true };

        // Act
        asset.SetFieldsForIngestion();

        // Assert
        asset.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void SetFieldsForIngestion_ClearsFields_Adjunct()
    {
        // Arrange
        var asset = new Adjunct
        {
            Error = "I am an error", Ingesting = false,
            Id = "someAdjunctId",
            MediaType = "image/jpeg",
            IIIFLink = IIIFLinkType.SeeAlso,
            Type = "Image",
            AssetId = new AssetId(1, 2, "foo"),
            ExternalId = null!
        };
        var expected = new Adjunct
        {
            Error = string.Empty, Ingesting = true,
            Id = "someAdjunctId",
            MediaType = "image/jpeg",
            IIIFLink = IIIFLinkType.SeeAlso,
            Type = "Image",
            AssetId = new AssetId(1, 2, "foo"),
            ExternalId = null!
        };

        // Act
        asset.SetFieldsForIngestion();

        // Assert
        asset.Should().BeEquivalentTo(expected);
    }
}
