using Engine.Ingest.Models;

namespace Engine.Tests.Ingest.Models;

public class LegacyIngestEventTests
{
    [Fact]
    public void AssetJson_Null_IfDictionaryNull()
    {
        // Arrange
        var evt = Create(null);

        // Act
        var assetJson = evt.AssetJson;

        // Assert
        assetJson.Should().BeNullOrEmpty();
    }

    [Fact]
    public void AssetJson_Null_IfDictionaryEmpty()
    {
        // Arrange
        var evt = Create(new Dictionary<string, string>());

        // Act
        var assetJson = evt.AssetJson;

        // Assert
        assetJson.Should().BeNullOrEmpty();
    }

    [Fact]
    public void AssetJson_Null_IfDictionaryDoesNotContainCorrectElement()
    {
        // Arrange
        var paramsDict = new Dictionary<string, string> { ["foo"] = "bar" };
        var evt = Create(paramsDict);

        // Act
        var assetJson = evt.AssetJson;

        // Assert
        assetJson.Should().BeNullOrEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("something")]
    public void AssetJson_ReturnsExpected_IfDictionaryContainCorrectElement(string value)
    {
        // Arrange
        var paramsDict = new Dictionary<string, string> { ["image"] = value };
        var evt = Create(paramsDict);

        // Act
        var assetJson = evt.AssetJson;

        // Assert
        assetJson.Should().Be(value);
    }

    private LegacyIngestEvent Create(Dictionary<string, string> paramsDict)
        => new("test", DateTime.Now, "test::type", paramsDict);
}