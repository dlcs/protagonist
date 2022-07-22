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

public class AssetNotificationTests
{
    private readonly IAssetNotificationSender sut;
    private readonly ControllableHttpMessageHandler httpHandler;

    public AssetNotificationTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        var httpClient = new HttpClient(httpHandler);
        var options = Options.Create(new DlcsSettings { EngineDirectIngestUri = new Uri("http://engine.dlcs/ingest") });
        var logger = new NullLogger<AssetNotificationSender>();
        sut = new AssetNotificationSender(httpClient, options, logger);
    }

    [Theory]
    [InlineData(AssetFamily.File, 'F')]
    [InlineData(AssetFamily.Image, 'I')]
    [InlineData(AssetFamily.Timebased, 'T')]
    public async Task AssetNotification_Sends_Legacy_Engine_Body(AssetFamily family, char expected)
    {
        // Arrange
        var asset = new Asset
        {
            Id = "99/1/ingest-asset",
            Customer = 99,
            Space = 1,
            Family = family
        };
        
        // This is the new format, accepted by IAssetNotificationSender
        var ingestRequest = new IngestAssetRequest(asset, DateTime.UtcNow);
        HttpRequestMessage message = null;
        httpHandler.RegisterCallback(r => message = r);
        httpHandler.GetResponseMessage("{ \"engine\": \"hello\" }", HttpStatusCode.OK);
        
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
        
        // Validate Family enum sent as char, rather than int
        JObject.Parse(jObj.SelectToken("params.image").Value<string>())["family"].Value<char>().Should().Be(expected);
    }
}