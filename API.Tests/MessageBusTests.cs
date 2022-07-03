using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DLCS.Core.Settings;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Repository.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Test.Helpers.Http;
using Xunit;

namespace API.Tests;

public class MessageBusTests
{
    private readonly IMessageBus sut;
    private readonly ControllableHttpMessageHandler httpHandler;

    public MessageBusTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        var httpClient = new HttpClient(httpHandler);
        var options = Options.Create(new DlcsSettings { EngineDirectIngestUri = new Uri("http://engine.dlcs/ingest") });
        var logger = new NullLogger<MessageBus>();
        sut = new MessageBus(httpClient, options, logger);
    }

    [Fact]
    public async Task MessageBus_Sends_Legacy_Engine_Body()
    {
        // Arrange
        var asset = new Asset
        {
            Id = "99/1/ingest-asset",
            Customer = 99,
            Space = 1
        };
        
        // This is the new format, accepted by IMessageBus
        var ingestRequest = new IngestAssetRequest(asset, DateTime.UtcNow);
        HttpRequestMessage message = null;
        httpHandler.RegisterCallback(r => message = r);
        var engineResponse = httpHandler.GetResponseMessage("{ \"engine\": \"hello\" }", HttpStatusCode.OK);
        
        // Act
        var statusCode = await sut.SendImmediateIngestAssetRequest(ingestRequest, false);
        
        // Assert
        statusCode.Should().Be(HttpStatusCode.OK);
        httpHandler.CallsMade.Should().ContainSingle()
            .Which.Should().Be("http://engine.dlcs/ingest");
        message.Method.Should().Be(HttpMethod.Post);
        var body = await message.Content.ReadAsStringAsync();
        
        // Our message to legacy Deliverator engine has been transformed to legacy engine format
        var jObj = JObject.Parse(body);
        jObj["_type"].Value<string>().Should().Be("event");
        jObj["message"].Value<string>().Should().Be("event::image-ingest");
        
        // This may be a bridge too far:
        var assetFromMessage = jObj["params"].ToObject<Asset>();
        assetFromMessage.Should().BeEquivalentTo(asset);
    }
    
}