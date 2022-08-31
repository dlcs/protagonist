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

namespace API.Tests;

public class AssetNotificationTests
{
    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly IQueueLookup queueLookup;
    private readonly IQueueSender queueSender;
    private readonly IHttpClientFactory httpClientFactory;

    public AssetNotificationTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        var httpClient = new HttpClient(httpHandler);
        
        httpClientFactory = A.Fake<IHttpClientFactory>();
        A.CallTo(() => httpClientFactory.CreateClient(A<string>._)).Returns(httpClient);

        queueLookup = A.Fake<IQueueLookup>();
        queueSender = A.Fake<IQueueSender>();
    }

    [Theory]
    [InlineData(AssetFamily.File, 'F')]
    [InlineData(AssetFamily.Image, 'I')]
    [InlineData(AssetFamily.Timebased, 'T')]
    public async Task SendImmediateIngestAssetRequest_Sends_Legacy_Engine_Body_IfUseLegacyEngineMessageTrue(
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
        var statusCode = await sut.SendImmediateIngestAssetRequest(ingestRequest, false);
        
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
    public async Task SendImmediateIngestAssetRequest_CallsEngineWithCurrentModel_IfUseLegacyEngineMessageFalse()
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
        var statusCode = await sut.SendImmediateIngestAssetRequest(ingestRequest, false);
        
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
    public async Task SendIngestAssetRequest_QueuesMessageWithLegacyModel_IfUseLegacyEngineMessageTrue()
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
        await sut.SendIngestAssetRequest(ingestRequest);
        
        // Assert
        var jObj = JObject.Parse(jsonString);
        jObj["_type"].Value<string>().Should().Be("event");
        jObj["message"].Value<string>().Should().Be("event::image-ingest");
    }
    
    [Fact]
    public async Task SendIngestAssetRequest_QueuesMessageWithCurrentModel_IfUseLegacyEngineMessageFalse()
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
        await sut.SendIngestAssetRequest(ingestRequest);
        
        // Assert
        var body = JsonSerializer.Deserialize<IngestAssetRequest>(jsonString,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        body.Should().BeEquivalentTo(ingestRequest);
    }

    private AssetNotificationSender GetSut(bool useLegacyMessageFormat)
    {
        var options = Options.Create(new DlcsSettings
        {
            EngineDirectIngestUri = new Uri("http://engine.dlcs/ingest"),
            UseLegacyEngineMessage = useLegacyMessageFormat
        });
        
        return new AssetNotificationSender(httpClientFactory, queueLookup, queueSender, options,
            new NullLogger<AssetNotificationSender>());
    }
}