using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using DLCS.AWS.SQS;
using DLCS.Core.Settings;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Repository.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
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
        
        var engineClientOptions = Options.Create(new DlcsSettings
        {
            EngineRoot = new Uri("http://engine.dlcs/")
        });
        
        sut = new EngineClient(queueLookup, queueSender, httpClient, engineClientOptions, new NullLogger<EngineClient>());
    }
    
    [Fact]
    public async Task SynchronousIngest_CallsEngine()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("99/1/ingest-asset"))
        {
            Family = AssetFamily.Image,
            Tags = "whatever",
            Roles = "secure",
            NumberReference1 = 1234
        };
        
        var ingestRequest = new IngestAssetRequest(asset.Id, DateTime.UtcNow);
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
    }
    
    [Fact]
    public async Task AsynchronousIngest_QueuesMessage()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("99/1/ingest-asset"))
        {
            Family = AssetFamily.Image,
            Tags = "whatever",
            Roles = "secure",
            NumberReference1 = 1234
        };
        
        var ingestRequest = new IngestAssetRequest(asset.Id, DateTime.UtcNow);
       
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
        httpHandler.CallsMade.Should().ContainSingle().Which.Should().Be("http://engine.dlcs/allowed-av");
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
        httpHandler.CallsMade.Should().ContainSingle().Which.Should().Be("http://engine.dlcs/allowed-av");
        message.Method.Should().Be(HttpMethod.Get);
        returnedAvPolicyOptions.Should().BeNull();
    }
}