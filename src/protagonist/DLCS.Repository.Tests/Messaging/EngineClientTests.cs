using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using DLCS.AWS.SQS;
using DLCS.AWS.Transcoding.Models;
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
using static DLCS.AWS.SQS.SqsQueueUtilities.Constants.MessageAttributeNames;

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

        sut = new EngineClient(queueLookup, queueSender, httpClient, new MockCachingService(),
            Options.Create(new CacheSettings()),
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
        A.CallTo(() => queueSender.QueueMessage("test-queue", A<string>._,
                A<Dictionary<string, string>>._, A<CancellationToken>._))
            .Invokes((string _, string message, IDictionary<string,string> _, CancellationToken _) => jsonString = message)
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
    public async Task AsynchronousIngest_QueuesMessage_ForAdjunct()
    {
        // Arrange
        var adjunct = new Adjunct
        {
            Id = "adjunct-1",
            AssetId = AssetId.FromString("99/1/ingest-asset"),
            MediaType = "text/plain",
            IIIFLink = IIIFLinkType.SeeAlso,
            Type = "test-type"
        };

        var capturedJson = string.Empty;
        var capturedAttributes = new Dictionary<string, string>();
        A.CallTo(() => queueLookup.GetAdjunctsQueueName()).Returns("adjunct-queue");
        A.CallTo(() => queueSender.QueueMessage("adjunct-queue", A<string>._, A<Dictionary<string, string>>._, A<CancellationToken>._))
            .Invokes((string _, string message, IDictionary<string, string> attrs, CancellationToken _) =>
            {
                capturedJson = message;
                foreach (var kvp in attrs) capturedAttributes[kvp.Key] = kvp.Value;
            })
            .Returns(true);

        // Act
        await sut.AsynchronousIngest(adjunct);

        // Assert
        A.CallTo(() => queueLookup.GetAdjunctsQueueName()).MustHaveHappenedOnceExactly();

        var body = JsonSerializer.Deserialize<IngestAdjunctRequest>(capturedJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        body!.Id.Should().Be(adjunct.Id);
        body.AssetId.Should().Be(adjunct.AssetId);

        capturedAttributes.Should().ContainKey(IngestType).WhoseValue.Should().Be(IngestAdjunctRequest.IngestType);
    }

    [Fact]
    public async Task AsynchronousIngestBatch_Asset_UsesDefaultQueue_WhenNotPriority()
    {
        // Arrange
        var assets = new List<Asset>
        {
            new(AssetId.FromString("99/1/asset-1")) { Family = AssetFamily.Image, Batch = 10 },
            new(AssetId.FromString("99/1/asset-2")) { Family = AssetFamily.Image, Batch = 10 }
        };

        IReadOnlyCollection<string> capturedMessages = [];
        IDictionary<string, string> capturedAttributes = new Dictionary<string, string>();
        A.CallTo(() => queueLookup.GetQueueNameForFamily(AssetFamily.Image, false)).Returns("default-queue");
        A.CallTo(() => queueSender.QueueMessages("default-queue", A<IReadOnlyCollection<string>>._, A<string>._,
                A<IDictionary<string, string>>._, A<CancellationToken>._))
            .Invokes((string _, IReadOnlyCollection<string> msgs, string _, IDictionary<string, string> attrs, CancellationToken _) =>
            {
                capturedMessages = msgs;
                capturedAttributes = attrs;
            })
            .Returns(2);

        // Act
        var result = await sut.AsynchronousIngestBatch(assets, false, CancellationToken.None);

        // Assert
        result.Should().Be(2);
        A.CallTo(() => queueLookup.GetQueueNameForFamily(AssetFamily.Image, false)).MustHaveHappenedOnceExactly();
        capturedMessages.Should().HaveCount(2);
        capturedAttributes.Should().ContainKey(IngestType).WhoseValue.Should().Be(IngestAssetRequest.IngestType);
    }

    [Fact]
    public async Task AsynchronousIngestBatch_Asset_UsesPriorityQueue_WhenPriority()
    {
        // Arrange
        var assets = new List<Asset>
        {
            new(AssetId.FromString("99/1/asset-1")) { Family = AssetFamily.Image, Batch = 10 }
        };

        A.CallTo(() => queueLookup.GetQueueNameForFamily(AssetFamily.Image, true)).Returns("priority-queue");
        A.CallTo(() => queueSender.QueueMessages("priority-queue", A<IReadOnlyCollection<string>>._, A<string>._,
                A<IDictionary<string, string>>._, A<CancellationToken>._))
            .Returns(1);

        // Act
        await sut.AsynchronousIngestBatch(assets, true, CancellationToken.None);

        // Assert
        A.CallTo(() => queueLookup.GetQueueNameForFamily(AssetFamily.Image, true)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AsynchronousIngestBatch_Asset_GroupsByFamily_SendingSeparateBatches()
    {
        // Arrange
        var assets = new List<Asset>
        {
            new(AssetId.FromString("99/1/image-1")) { Family = AssetFamily.Image, Batch = 10 },
            new(AssetId.FromString("99/1/timebased-1")) { Family = AssetFamily.Timebased, Batch = 10 }
        };

        A.CallTo(() => queueLookup.GetQueueNameForFamily(AssetFamily.Image, false)).Returns("image-queue");
        A.CallTo(() => queueLookup.GetQueueNameForFamily(AssetFamily.Timebased, false)).Returns("timebased-queue");
        A.CallTo(() => queueSender.QueueMessages(A<string>._, A<IReadOnlyCollection<string>>._, A<string>._,
                A<IDictionary<string, string>>._, A<CancellationToken>._))
            .Returns(1);

        // Act
        var result = await sut.AsynchronousIngestBatch(assets, false, CancellationToken.None);

        // Assert
        result.Should().Be(2);
        A.CallTo(() => queueSender.QueueMessages("image-queue", A<IReadOnlyCollection<string>>._, A<string>._,
                A<IDictionary<string, string>>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => queueSender.QueueMessages("timebased-queue", A<IReadOnlyCollection<string>>._, A<string>._,
                A<IDictionary<string, string>>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AsynchronousIngestBatch_Adjunct_UsesAdjunctQueue()
    {
        // Arrange
        var adjuncts = new List<Adjunct>
        {
            new() { Id = "adj-1", AssetId = AssetId.FromString("99/1/asset-1"), MediaType = "text/plain", IIIFLink = IIIFLinkType.SeeAlso, Type = "t", Batch = 5 },
            new() { Id = "adj-2", AssetId = AssetId.FromString("99/1/asset-1"), MediaType = "text/plain", IIIFLink = IIIFLinkType.SeeAlso, Type = "t", Batch = 5 }
        };

        IReadOnlyCollection<string> capturedMessages = [];
        IDictionary<string, string> capturedAttributes = new Dictionary<string, string>();
        A.CallTo(() => queueLookup.GetAdjunctsQueueName()).Returns("adjunct-queue");
        A.CallTo(() => queueSender.QueueMessages("adjunct-queue", A<IReadOnlyCollection<string>>._, A<string>._,
                A<IDictionary<string, string>>._, A<CancellationToken>._))
            .Invokes((string _, IReadOnlyCollection<string> msgs, string _, IDictionary<string, string> attrs, CancellationToken _) =>
            {
                capturedMessages = msgs;
                capturedAttributes = attrs;
            })
            .Returns(2);

        // Act
        var result = await sut.AsynchronousIngestBatch(adjuncts);

        // Assert
        result.Should().Be(2);
        A.CallTo(() => queueLookup.GetAdjunctsQueueName()).MustHaveHappenedOnceExactly();
        capturedMessages.Should().HaveCount(2);
        capturedAttributes.Should().ContainKey(IngestType).WhoseValue.Should().Be(IngestAdjunctRequest.IngestType);
    }

    [Fact]
    public async Task GetAllowedAvOptions_RetrievesAllowedAvPolicies()
    {
        // Arrange
        HttpRequestMessage message = null;
        httpHandler.RegisterCallback(r => message = r);
        httpHandler.GetResponseMessage("[\"video-mp4-480p\",\"video-webm-720p\",\"audio-mp3-128k\"]",
            HttpStatusCode.OK);

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
