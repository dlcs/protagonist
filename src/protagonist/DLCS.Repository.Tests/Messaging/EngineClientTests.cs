using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.SQS;
using DLCS.Core.Settings;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Repository.Messaging;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Test.Helpers.Http;
using Xunit;

namespace DLCS.Repository.Tests.Messaging;

public class EngineClientTests
{
    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly IQueueLookup queueLookup;
    private readonly IQueueSender queueSender;
    private readonly HttpClient httpClient;

    public EngineClientTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        httpClient = new HttpClient(httpHandler);

        queueLookup = A.Fake<IQueueLookup>();
        queueSender = A.Fake<IQueueSender>();
    }

    [Theory]
    [InlineData(AssetFamily.File, 'F')]
    [InlineData(AssetFamily.Image, 'I')]
    [InlineData(AssetFamily.Timebased, 'T')]
    public async Task SynchronousIngest_CallsEngineWithLegacyModel_IfUseLegacyEngineMessageTrue(
        AssetFamily family, char expected)
    {
        // Arrange
        var asset = new Asset
        {
            Id = "99/1/ingest-asset",
            Customer = 99,
            Space = 1,
            Family = family
        };
        
        var ingestRequest = new IngestAssetRequest(asset, DateTime.UtcNow);
        HttpRequestMessage message = null;
        httpHandler.RegisterCallback(r => message = r);
        httpHandler.GetResponseMessage("{ \"engine\": \"hello\" }", HttpStatusCode.OK);

        var sut = GetSut(true);
        
        // Act
        var statusCode = await sut.SynchronousIngest(ingestRequest, false);
        
        // Assert
        statusCode.Should().Be(HttpStatusCode.OK);
        httpHandler.CallsMade.Should().ContainSingle().Which.Should().Be("http://engine.dlcs/ingest");
        message.Method.Should().Be(HttpMethod.Post);

        var body = await message.Content.ReadAsStringAsync();
        var jObj = JObject.Parse(body);
        jObj["_type"].Value<string>().Should().Be("event");
        jObj["message"].Value<string>().Should().Be("event::image-ingest");
        
        // Validate Family enum sent as char, rather than int
        JObject.Parse(jObj.SelectToken("params.image").Value<string>())["family"].Value<char>().Should().Be(expected);
    }
    
    [Fact]
    public async Task SynchronousIngest_CallsEngineWithCurrentModel_IfUseLegacyEngineMessageFalse()
    {
        // Arrange
        var asset = new Asset
        {
            Id = "99/1/ingest-asset",
            Customer = 99,
            Space = 1,
            Family = AssetFamily.Image,
            Tags = "whatever",
            Roles = "secure",
            NumberReference1 = 1234
        };
        
        var ingestRequest = new IngestAssetRequest(asset, DateTime.UtcNow);
        HttpRequestMessage message = null;
        httpHandler.RegisterCallback(r => message = r);
        httpHandler.GetResponseMessage("{ \"engine\": \"hello\" }", HttpStatusCode.OK);

        var sut = GetSut(false);
        
        // Act
        var statusCode = await sut.SynchronousIngest(ingestRequest, false);
        
        // Assert
        statusCode.Should().Be(HttpStatusCode.OK);
        httpHandler.CallsMade.Should().ContainSingle().Which.Should().Be("http://engine.dlcs/ingest");
        message.Method.Should().Be(HttpMethod.Post);

        var jsonContents = await message.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<IngestAssetRequest>(jsonContents,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        body.Should().BeEquivalentTo(ingestRequest);
    }
    
    [Fact]
    public async Task AsynchronousIngest_QueuesMessageWithLegacyModel_IfUseLegacyEngineMessageTrue()
    {
        // Arrange
        var asset = new Asset
        {
            Id = "99/1/ingest-asset",
            Customer = 99,
            Space = 1,
            Family = AssetFamily.Image
        };
        
        var ingestRequest = new IngestAssetRequest(asset, DateTime.UtcNow);

        var sut = GetSut(true);
        string jsonString = string.Empty;
        A.CallTo(() => queueLookup.GetQueueNameForFamily(AssetFamily.Image, false)).Returns("test-queue");
        A.CallTo(() => queueSender.QueueMessage("test-queue", A<string>._, A<CancellationToken>._))
            .Invokes((string _, string message, CancellationToken _) => jsonString = message)
            .Returns(true);
        
        // Act
        await sut.AsynchronousIngest(ingestRequest);
        
        // Assert
        var jObj = JObject.Parse(jsonString);
        jObj["_type"].Value<string>().Should().Be("event");
        jObj["message"].Value<string>().Should().Be("event::image-ingest");
    }
    
    [Fact]
    public async Task AsynchronousIngest_QueuesMessageWithCurrentModel_IfUseLegacyEngineMessageFalse()
    {
        // Arrange
        var asset = new Asset
        {
            Id = "99/1/ingest-asset",
            Customer = 99,
            Space = 1,
            Family = AssetFamily.Image,
            Tags = "whatever",
            Roles = "secure",
            NumberReference1 = 1234
        };
        
        var ingestRequest = new IngestAssetRequest(asset, DateTime.UtcNow);
        var sut = GetSut(false);

        string jsonString = string.Empty;
        A.CallTo(() => queueLookup.GetQueueNameForFamily(AssetFamily.Image, false)).Returns("test-queue");
        A.CallTo(() => queueSender.QueueMessage("test-queue", A<string>._, A<CancellationToken>._))
            .Invokes((string _, string message, CancellationToken _) => jsonString = message)
            .Returns(true);

        // Act
        await sut.AsynchronousIngest(ingestRequest);
        
        // Assert
        var body = JsonSerializer.Deserialize<IngestAssetRequest>(jsonString,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        body.Should().BeEquivalentTo(ingestRequest);
    }

    private EngineClient GetSut(bool useLegacyMessageFormat)
    {
        var options = Options.Create(new DlcsSettings
        {
            EngineDirectIngestUri = new Uri("http://engine.dlcs/ingest"),
            UseLegacyEngineMessage = useLegacyMessageFormat
        });

        return new EngineClient(queueLookup, queueSender, httpClient, options, new NullLogger<EngineClient>());
    }
}