using DLCS.Model.Assets;
using Engine.Ingest.Models;

namespace Engine.Tests.Ingest.Models;

public class IngestRequestConverterTests
{
    [Fact]
    public void ConvertToInternalRequest_Throws_IfIncomingRequestNull()
    {
        // Arrange
        LegacyIngestEvent? request = null;

        // Act
        Action action = () => request.ConvertToAssetRequest();

        // Assert
        action.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'incomingRequest')");
    }

    [Fact]
    public void ConvertToInternalRequest_Throws_IfIncomingRequestDoesNotContainAssetJson()
    {
        // Arrange
        var request = Create(new Dictionary<string, string>());

        // Act
        Action action = () => request.ConvertToAssetRequest();

        // Assert
        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Cannot convert LegacyIngestEvent that has no Asset Json");
    }

    [Fact]
    public void ConvertToInternalRequest_Throws_IfIncomingRequestContainsAssetJson_InInvalidFormat()
    {
        // Arrange
        const string assetJson = "i-am-not-json{}";
        var paramsDict = new Dictionary<string, string> { ["image"] = assetJson };
        var request = Create(paramsDict);

        // Act
        Action action = () => request.ConvertToAssetRequest();

        // Assert
        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Unable to deserialize Asset Json from LegacyIngestEvent");
    }

    [Theory]
    [InlineData(
        "{\"id\": \"2/1/engine-9\",\"customer\": 2,\"space\": 1,\"rawId\": \"engine-9\",\"created\": \"2020-04-09T00:00:00\",\"origin\": \"https://burst.shopifycdn.com/photos/chrome-engine-close-up.jpg\",\"tags\": [\"one\"],\"roles\": [\"https://api.dlcs.digirati.io/customers/2/roles/clickthrough\"  ],  \"preservedUri\": \"\",  \"string1\": \"foo\",  \"string2\": \"bar\",  \"string3\": \"baz\",  \"maxUnauthorised\": 300,  \"number1\": 10,  \"number2\": 20,  \"number3\": 30,  \"width\": 100,  \"height\": 200,  \"duration\": 90,\"error\": \"\",\"batch\": 999,\"finished\": null,\"ingesting\": false,\"imageOptimisationPolicy\": \"fast-higher\",\"thumbnailPolicy\": \"default\",\"family\": \"I\",\"mediaType\": \"image/jp2\"}")]
    [InlineData(
        "{\r\n  \"id\": \"2/1/engine-9\",\r\n  \"customer\": 2,\r\n  \"space\": 1,\r\n  \"rawId\": \"engine-9\",\r\n  \"created\": \"2020-04-09T00:00:00\",\r\n  \"origin\": \"https://burst.shopifycdn.com/photos/chrome-engine-close-up.jpg\",\r\n  \"tags\": [\r\n  \"one\"],\r\n  \"roles\": [\r\n    \"https://api.dlcs.digirati.io/customers/2/roles/clickthrough\"\r\n  ],\r\n  \"preservedUri\": \"\",\r\n  \"string1\": \"foo\",\r\n  \"string2\": \"bar\",\r\n  \"string3\": \"baz\",\r\n  \"maxUnauthorised\": 300,\r\n  \"number1\": 10,\r\n  \"number2\": 20,\r\n  \"number3\": 30,\r\n  \"width\": 100,\r\n  \"height\": 200,\r\n  \"duration\": 90,\r\n  \"error\": \"\",\r\n  \"batch\": 999,\r\n  \"finished\": null,\r\n  \"ingesting\": false,\r\n  \"imageOptimisationPolicy\": \"fast-higher\",\r\n  \"thumbnailPolicy\": \"default\",\r\n  \"family\": \"I\",\r\n  \"mediaType\": \"image/jp2\"\r\n}")]
    public void ConvertToInternalRequest_ReturnsExpected(string assetJson)
    {
        // Arrange
        var paramsDict = new Dictionary<string, string> { ["image"] = assetJson };
        var request = Create(paramsDict);
        var created = new DateTime(2020, 04, 09);
        DateTime.SpecifyKind(created, DateTimeKind.Utc);
        var expected = new Asset
        {
            Id = "2/1/engine-9", Customer = 2, Space = 1, Created = created,
            Origin = "https://burst.shopifycdn.com/photos/chrome-engine-close-up.jpg", Tags = "one",
            Roles = "https://api.dlcs.digirati.io/customers/2/roles/clickthrough", PreservedUri = "", Reference1 = "foo",
            Reference2 = "bar", Reference3 = "baz", MaxUnauthorised = 300, NumberReference1 = 10, NumberReference2 = 20,
            NumberReference3 = 30, Width = 100, Height = 200, Duration = 90, Error = string.Empty, Batch = 999, 
            Finished = null, Ingesting = false, ImageOptimisationPolicy = "fast-higher", ThumbnailPolicy = "default",
            Family = AssetFamily.Image, MediaType = "image/jp2"
        };

        // Act
        var result = request.ConvertToAssetRequest();

        // Assert
        result.Asset.Should().BeEquivalentTo(expected);
    }

    private LegacyIngestEvent Create(Dictionary<string, string> paramsDict)
        => new("test", DateTime.Now, "test::type", paramsDict);
}