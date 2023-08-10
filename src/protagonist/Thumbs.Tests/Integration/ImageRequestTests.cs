using System.Net;
using System.Text.Json.Nodes;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using Test.Helpers.Integration;
using Thumbs.Tests.Integration.Infrastructure;

namespace Thumbs.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class ImageRequestTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;

    public ImageRequestTests(ProtagonistAppFactory<Startup> factory, DlcsDatabaseFixture dbFixture)
    {
        this.dbFixture = dbFixture;
        httpClient = factory
            .WithConnectionString(this.dbFixture.ConnectionString)
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        this.dbFixture.CleanUp();
    }
    
    [Theory]
    [InlineData("tif")]
    [InlineData("png")]
    [InlineData("gif")]
    [InlineData("jp2")]
    [InlineData("pdf")]
    [InlineData("webp")]
    public async Task GetThumbnail_Returns400_IfUnsupportedFormatRequested(string format)
    {
        // Arrange
        var id = $"99/1/{nameof(GetThumbnail_Returns400_IfUnsupportedFormatRequested)}";

        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/full/200,150/0/default.{format}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        var responseObject = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        responseObject["message"].ToString().Should().Be($"Requested format '{format}' not supported, use 'jpg'");
        responseObject["statusCode"].ToString().Should().Be("400");
    }

    [Theory]
    [InlineData("bitonal")]
    [InlineData("gray")]
    public async Task GetThumbnail_Returns400_IfUnsupportedQualityRequested(string quality)
    {
        // Arrange
        var id = $"99/1/{nameof(GetThumbnail_Returns400_IfUnsupportedQualityRequested)}";

        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/full/max/0/{quality}.jpg");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        var responseObject = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        responseObject["message"].ToString().Should()
            .Be($"Requested quality '{quality}' not supported, use 'default' or 'color'");
        responseObject["statusCode"].ToString().Should().Be("400");
    }
    
    [Theory]
    [InlineData("!0")]
    [InlineData("90")]
    public async Task GetThumbnail_Returns400_IfUnsupportedRotationRequested(string rotation)
    {
        // Arrange
        var id = $"99/1/{nameof(GetThumbnail_Returns400_IfUnsupportedRotationRequested)}";

        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/full/max/{rotation}/default.jpg");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        var responseObject = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        responseObject["message"].ToString().Should()
            .Be($"Requested rotation value not supported, use '0'");
        responseObject["statusCode"].ToString().Should().Be("400");
    }
}