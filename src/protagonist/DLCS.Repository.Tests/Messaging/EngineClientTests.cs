using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using DLCS.AWS.ElasticTranscoder.Models;
using DLCS.AWS.SQS;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Repository.Messaging;
using FakeItEasy;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Http;

namespace DLCS.Repository.Tests.Messaging;

public class EngineClientTests
{
    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly IQueueLookup queueLookup;
    private readonly IQueueSender queueSender;
    private readonly HttpClient httpClient;
    private readonly EngineClient sut;
    
    public EngineClientTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("http://engine.dlcs")
        };

        queueLookup = A.Fake<IQueueLookup>();
        queueSender = A.Fake<IQueueSender>();

        sut = new EngineClient(queueLookup, queueSender, httpClient, new MockCachingService(), Options.Create(new CacheSettings()),
            new NullLogger<EngineClient>());
    }
    
    [Theory]
    [InlineData(123)]
    [InlineData(null)]
    public async Task SynchronousIngest_CallsEngine(int? batchId)
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("99/1/ingest-asset"))
        {
            Family = AssetFamily.Image,
            Tags = "whatever",
            Roles = "secure",
            NumberReference1 = 1234,
            Batch = batchId
        };
        
        var ingestRequest = new IngestAssetRequest(asset.Id, DateTime.UtcNow, batchId);
        HttpRequestMessage message = null;
        httpHandler.RegisterCallback(r => message = r);
        httpHandler.GetResponseMessage("{ \"engine\": \"hello\" }", HttpStatusCode.OK);

        // Act
        var statusCode = await sut.SynchronousIngest(asset);
        
        // Assert
        statusCode.Should().Be(HttpStatusCode.OK);
        httpHandler.CallsMade.Should().ContainSingle().Which.Should().Be("http://engine.dlcs/asset-ingest");
        message.Method.Should().Be(HttpMethod.Post);

        var jsonContents = await message.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<IngestAssetRequest>(jsonContents,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                ReferenceHandler = ReferenceHandler.Preserve
            });
        
        body.Id.Should().Be(ingestRequest.Id);
        body.BatchId.Should().Be(batchId);
    }
    
    [Theory]
    [InlineData(123)]
    [InlineData(null)]
    public async Task AsynchronousIngest_QueuesMessage(int? batchId)
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("99/1/ingest-asset"))
        {
            Family = AssetFamily.Image,
            Tags = "whatever",
            Roles = "secure",
            NumberReference1 = 1234,
            Batch = batchId
        };
        
        var ingestRequest = new IngestAssetRequest(asset.Id, DateTime.UtcNow, batchId);
       
        var jsonString = string.Empty;
        A.CallTo(() => queueLookup.GetQueueNameForFamily(AssetFamily.Image, false)).Returns("test-queue");
        A.CallTo(() => queueSender.QueueMessage("test-queue", A<string>._, A<CancellationToken>._))
            .Invokes((string _, string message, CancellationToken _) => jsonString = message)
            .Returns(true);

        // Act
        await sut.AsynchronousIngest(asset);
        
        // Assert
        var body = JsonSerializer.Deserialize<IngestAssetRequest>(jsonString,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReferenceHandler = ReferenceHandler.Preserve
        });

        body.Id.Should().Be(ingestRequest.Id);
        body.BatchId.Should().Be(batchId);
    }
    
    [Fact]
    public async Task GetAllowedAvOptions_RetrievesAllowedAvPolicies()
    {
        // Arrange
        HttpRequestMessage message = null;
        httpHandler.RegisterCallback(r => message = r);
        httpHandler.GetResponseMessage("[\"video-mp4-480p\",\"video-webm-720p\",\"audio-mp3-128k\"]", HttpStatusCode.OK);
        
        // Act
        var returnedAvPolicyOptions = await sut.GetAllowedAvPolicyOptions();
        
        // Assert
        httpHandler.CallsMade.Should().ContainSingle().Which.Should().Be("http://engine.dlcs/av/allowed");
        message.Method.Should().Be(HttpMethod.Get);
        returnedAvPolicyOptions!.Count.Should().Be(3);
        returnedAvPolicyOptions!.Should().BeEquivalentTo("video-mp4-480p", "video-webm-720p", "audio-mp3-128k");
    }
    
    [Fact]
    public async Task GetAllowedAvOptions_ReturnsNull_IfEngineAvPolicyEndpointUnreachable()
    {
        // Arrange
        HttpRequestMessage message = null;
        httpHandler.RegisterCallback(r => message = r);
        httpHandler.GetResponseMessage("Not found", HttpStatusCode.NotFound);
        
        // Act
        var returnedAvPolicyOptions = await sut.GetAllowedAvPolicyOptions();
        
        // Assert
        httpHandler.CallsMade.Should().ContainSingle().Which.Should().Be("http://engine.dlcs/av/allowed");
        message.Method.Should().Be(HttpMethod.Get);
        returnedAvPolicyOptions.Should().BeNull();
    }
    
    [Fact]
    public async Task GetAvPresets_RetrievesAllowedAvPresets()
    {
        // Arrange
        HttpRequestMessage message = null;
        httpHandler.RegisterCallback(r => message = r);
        
        var response = JsonSerializer.Serialize(new Dictionary<string, TranscoderPreset>()
        {
            { "webm-policy", new("webm-policy", "some-webm-preset", "oga") },
            { "oga-policy", new("oga-policy", "some-oga-preset", "webm") }
        });
        
        httpHandler.GetResponseMessage(response, HttpStatusCode.OK);
        
        // Act
        var returnedAvPresets = await sut.GetAvPresets();
        
        // Assert
        httpHandler.CallsMade.Should().ContainSingle().Which.Should().Be("http://engine.dlcs/av/presets");
        message.Method.Should().Be(HttpMethod.Get);
        returnedAvPresets!.Count.Should().Be(2);
        returnedAvPresets!.Keys.Should().BeEquivalentTo("webm-policy", "oga-policy");
        returnedAvPresets!.Values.Should().Contain(new TranscoderPreset("webm-policy", "some-webm-preset", "oga"));
    }
    
    [Fact]
    public async Task GetAvPresets_ReturnsEmpty_IfEngineAvPolicyEndpointThrowsError()
    {
        // Arrange
        httpHandler.RegisterCallback(r => throw new Exception("error"));
        httpHandler.GetResponseMessage("Not found", HttpStatusCode.NotFound);
        
        // Act
        var returnedAvPresets = await sut.GetAvPresets();
        
        // Assert
        httpHandler.CallsMade.Should().ContainSingle().Which.Should().Be("http://engine.dlcs/av/presets");
        returnedAvPresets.Should().BeEmpty();
    }
}
