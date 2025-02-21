using System.Net;
using System.Text;
using System.Text.Json;
using DLCS.Core.Types;
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
    [InlineData(IngestResultStatus.Unknown, HttpStatusCode.InternalServerError)]
    [InlineData(IngestResultStatus.Failed, HttpStatusCode.InternalServerError)]
    [InlineData(IngestResultStatus.Success, HttpStatusCode.OK)]
    [InlineData(IngestResultStatus.QueuedForProcessing, HttpStatusCode.Accepted)]
    [InlineData(IngestResultStatus.StorageLimitExceeded, HttpStatusCode.InsufficientStorage)]
    public async Task IngestAsset_ReturnsExpectedCode_ForIngestResult(IngestResultStatus ingestResult, HttpStatusCode expected)
    {
        // Arrange
        var assetId = AssetId.FromString($"1/2/{ingestResult}");
        var message = new IngestAssetRequest(assetId, DateTime.UtcNow, null);
        A.CallTo(() =>
            assetIngester.Ingest(A<IngestAssetRequest>.That.Matches(r => r.Id == assetId),
                A<CancellationToken>._)).Returns(new IngestResult(null, ingestResult));

        // Act
        var result = await httpClient.PostAsync("asset-ingest", GetJsonContent(message));
        
        // Assert
        result.StatusCode.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(IngestResultStatus.Unknown, HttpStatusCode.InternalServerError)]
    [InlineData(IngestResultStatus.Failed, HttpStatusCode.InternalServerError)]
    [InlineData(IngestResultStatus.Success, HttpStatusCode.OK)]
    [InlineData(IngestResultStatus.QueuedForProcessing, HttpStatusCode.Accepted)]
    [InlineData(IngestResultStatus.StorageLimitExceeded, HttpStatusCode.InsufficientStorage)]
    public async Task IngestAsset_ReturnsExpectedCode_ForIngestResult_ByteArray(IngestResultStatus ingestResult,
        HttpStatusCode expected)
    {
        // Arrange
        var assetId = AssetId.FromString($"1/2/{ingestResult}");
        var message = new IngestAssetRequest(assetId, DateTime.UtcNow, null);
        A.CallTo(() =>
            assetIngester.Ingest(A<IngestAssetRequest>.That.Matches(r => r.Id == assetId),
                A<CancellationToken>._)).Returns(new IngestResult(null, ingestResult));

        // Act
        var result = await httpClient.PostAsync("asset-ingest", GetByteArrayContent(message));

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