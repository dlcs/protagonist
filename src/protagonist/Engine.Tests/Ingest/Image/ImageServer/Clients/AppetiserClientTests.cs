using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DLCS.Core.Types;
using Engine.Ingest.Image.ImageServer.Clients;
using Engine.Ingest.Image.ImageServer.Models;
using Engine.Settings;
using Test.Helpers.Http;
using Test.Helpers.Settings;

namespace Engine.Tests.Ingest.Image.ImageServer.Clients;

public class AppetiserClientTests
{
    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly IAppetiserClient sut;
    private static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.Web);
    
    public AppetiserClientTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        var engineSettings = new EngineSettings
        {
            ImageIngest = new ImageIngestSettings
            {
                ScratchRoot = "scratch/",
                DestinationTemplate ="{root}{customer}/{space}/{image}/output",
                SourceTemplate = "source/",
                ThumbsTemplate = "thumb/"
            }
        };
        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(engineSettings);

        var httpClient = new HttpClient(httpHandler);
        httpClient.BaseAddress = new Uri("http://image-processor/");
        sut = new AppetiserClient(httpClient, optionsMonitor);
    }

    [Fact]
    public async Task GenerateJpeg2000_ReturnsSuccessfulAppetiserResponse_WhenSuccess()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
        };

        var response = httpHandler.GetResponseMessage(JsonSerializer.Serialize(imageProcessorResponse, Settings),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);
        var context = IngestionContextFactory.GetIngestionContext();

        // Act
        var appetiserResponse = await sut.GenerateJP2(context, new AssetId(1, 2, "stuff/asset"));

        var convertedAppetiserResponse = appetiserResponse as AppetiserResponseModel;

        // Assert
        convertedAppetiserResponse.Height.Should().Be(imageProcessorResponse.Height);
        convertedAppetiserResponse.Width.Should().Be(imageProcessorResponse.Width);
    }
    
    [Fact]
    public async Task GenerateJpeg2000_ReturnsErrorAppetiserResponse_WhenNotSuccess()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseErrorModel()
        {
            Message = "Error",
            Status = "Error"
        };

        var response = httpHandler.GetResponseMessage(JsonSerializer.Serialize(imageProcessorResponse, Settings),
            HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);
        var context = IngestionContextFactory.GetIngestionContext();
        
        // Act
        var appetiserResponse = await sut.GenerateJP2(context, new AssetId(1, 2, "stuff/asset"));

        var convertedAppetiserResponse = appetiserResponse as AppetiserResponseErrorModel;

        // Assert
        convertedAppetiserResponse.Message.Should().Be(imageProcessorResponse.Message);
        convertedAppetiserResponse.Status.Should().Be(imageProcessorResponse.Status);
    }
}