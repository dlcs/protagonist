using System.Net;
using System.Text;
using System.Text.Json;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using Engine.Ingest;
using Engine.Ingest.Models;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Integration;

namespace Engine.Tests.Integration;

/// <summary>
/// Tests that verify the correct response is returned to user using a fake ingestor
/// </summary>
[Trait("Category", "Integration")]
public class IngestResponseTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    private static readonly IAssetIngester assetIngester = A.Fake<IAssetIngester>();
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web);
    
    public IngestResponseTests(ProtagonistAppFactory<Startup> appFactory)
    {
        httpClient = appFactory
            .WithTestServices(services =>
            {
                services.AddSingleton<IAssetIngester>(assetIngester);
            })
            .CreateClient();
    }
    
    [Theory]
    [InlineData(IngestResult.Unknown, HttpStatusCode.InternalServerError)]
    [InlineData(IngestResult.Failed, HttpStatusCode.InternalServerError)]
    [InlineData(IngestResult.Success, HttpStatusCode.OK)]
    [InlineData(IngestResult.QueuedForProcessing, HttpStatusCode.Accepted)]
    public async Task IngestAsset_ReturnsExpectedCode_ForIngestResult(IngestResult ingestResult, HttpStatusCode expected)
    {
        // Arrange
        var assetId = $"1/2/{ingestResult}";
        var message = new IngestAssetRequest(new Asset { Id = assetId }, DateTime.UtcNow);
        A.CallTo(() =>
            assetIngester.Ingest(A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId),
                A<CancellationToken>._)).Returns(ingestResult);

        // Act
        var result = await httpClient.PostAsync("asset-ingest", GetJsonContent(message));
        
        // Assert
        result.StatusCode.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(IngestResult.Unknown, HttpStatusCode.InternalServerError)]
    [InlineData(IngestResult.Failed, HttpStatusCode.InternalServerError)]
    [InlineData(IngestResult.Success, HttpStatusCode.OK)]
    [InlineData(IngestResult.QueuedForProcessing, HttpStatusCode.Accepted)]
    public async Task IngestImage_ReturnsExpectedCode_ForIngestResult_Legacy(IngestResult ingestResult, HttpStatusCode expected)
    {
        // Arrange
        var assetId = $"1/2/{ingestResult}";
        var message = new LegacyIngestEvent(
            assetId, 
            DateTime.UtcNow, 
            "message", 
            new Dictionary<string, string>
        {
            ["asset"] = "test"
        });
        
        A.CallTo(() =>
            assetIngester.Ingest(A<LegacyIngestEvent>.That.Matches(r => r.Type == assetId),
                A<CancellationToken>._)).Returns(ingestResult);

        // Act
        var result = await httpClient.PostAsync("image-ingest", GetJsonContent(message));
        
        // Assert
        result.StatusCode.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(IngestResult.Unknown, HttpStatusCode.InternalServerError)]
    [InlineData(IngestResult.Failed, HttpStatusCode.InternalServerError)]
    [InlineData(IngestResult.Success, HttpStatusCode.OK)]
    [InlineData(IngestResult.QueuedForProcessing, HttpStatusCode.Accepted)]
    public async Task IngestAsset_ReturnsExpectedCode_ForIngestResult_ByteArray(IngestResult ingestResult,
        HttpStatusCode expected)
    {
        // Arrange
        var assetId = $"1/2/{ingestResult}";
        var message = new IngestAssetRequest(new Asset { Id = assetId }, DateTime.UtcNow);
        A.CallTo(() =>
            assetIngester.Ingest(A<IngestAssetRequest>.That.Matches(r => r.Asset.Id == assetId),
                A<CancellationToken>._)).Returns(ingestResult);

        // Act
        var result = await httpClient.PostAsync("asset-ingest", GetByteArrayContent(message));

        // Assert
        result.StatusCode.Should().Be(expected);
    }

    [Theory]
    [InlineData(IngestResult.Unknown, HttpStatusCode.InternalServerError)]
    [InlineData(IngestResult.Failed, HttpStatusCode.InternalServerError)]
    [InlineData(IngestResult.Success, HttpStatusCode.OK)]
    [InlineData(IngestResult.QueuedForProcessing, HttpStatusCode.Accepted)]
    public async Task IngestImage_ReturnsExpectedCode_ForIngestResult_Legacy_ByteArray(IngestResult ingestResult,
        HttpStatusCode expected)
    {
        // Arrange
        var assetId = $"1/2/{ingestResult}";
        var message = new LegacyIngestEvent(
            assetId,
            DateTime.UtcNow,
            "message",
            new Dictionary<string, string>
            {
                ["asset"] = "test"
            });

        A.CallTo(() =>
            assetIngester.Ingest(A<LegacyIngestEvent>.That.Matches(r => r.Type == assetId),
                A<CancellationToken>._)).Returns(ingestResult);

        // Act
        var result = await httpClient.PostAsync("image-ingest", GetByteArrayContent(message));

        // Assert
        result.StatusCode.Should().Be(expected);
    }

    private StringContent GetJsonContent(object message)
    {
        var jsonString = JsonSerializer.Serialize(message, settings);
        return new StringContent(jsonString, Encoding.UTF8, "application/json");
    }

    private ByteArrayContent GetByteArrayContent(object message)
    {
        var jsonString = JsonSerializer.Serialize(message, settings);
        return new ByteArrayContent(Encoding.ASCII.GetBytes(jsonString));
    }
}