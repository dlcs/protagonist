using System.Net;
using Engine.Ingest.Image.ImageServer;
using Engine.Ingest.Image.ImageServer.Clients;
using Engine.Ingest.Image.ImageServer.Models;
using Engine.Settings;
using IIIF.ImageApi;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Data;
using Test.Helpers.Http;
using Test.Helpers.Settings;

namespace Engine.Tests.Ingest.Image.ImageServer.Clients;

public class AppetiserClientTests
{
    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly IImageProcessorClient sut;
    

    private readonly IReadOnlyList<SizeParameter> thumbnailSizes =
        [SizeParameter.Parse("!1024,1024"), SizeParameter.Parse("880,")];
    
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
        sut = new AppetiserClient(httpClient, optionsMonitor, NullLogger<AppetiserClient>.Instance);
    }
    
    [Fact]
    public async Task GenerateDerivatives_ReturnsError_IfNoImageProcessorOptions()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);
        
        // Act
        var response = await sut.GenerateDerivatives(context, assetId, thumbnailSizes, ImageProcessorOperations.None);

        // Assert
        var errorResponse = (AppetiserResponseErrorModel)response;
        errorResponse.Status.Should().Be("500");
        errorResponse.Message.Should().Be("You must specify an operation");
    }
    
    [Theory]
    [InlineData(ImageProcessorOperations.Thumbnails | ImageProcessorOperations.Derivative, "ingest")]
    [InlineData(ImageProcessorOperations.Thumbnails, "derivatives-only")]
    public async Task GenerateDerivatives_MakesCorrectRequest(ImageProcessorOperations operation, string passedValue)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);

        HttpRequestMessage message = null!;
        AppetiserRequestModel requestModel = null!;
        httpHandler.RegisterCallback(r =>
        {
            message = r;
            requestModel = message.Content.ReadAsAsync<AppetiserRequestModel>().Result;
        });

        // Act
        await sut.GenerateDerivatives(context, assetId, thumbnailSizes, operation);

        // Assert
        httpHandler.CallsMade.Should().ContainSingle().Which.Should().Be("http://image-processor/convert");
        message.Method.Should().Be(HttpMethod.Post);
        requestModel.Operation.Should().Be(passedValue);
    }

    [Fact]
    public async Task GenerateDerivatives_ReturnsSuccessfulAppetiserResponse_WhenSuccess()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel { Height = 1000, Width = 5000, };
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);

        var httpResponse = httpHandler.GetJsonResponseMessage(imageProcessorResponse, HttpStatusCode.OK);
        httpHandler.SetResponse(httpResponse);

        // Act
        var response =
            await sut.GenerateDerivatives(context, assetId, thumbnailSizes, ImageProcessorOperations.Derivative);
        
        // Assert
        var convertedResponse = (AppetiserResponseModel)response;
        convertedResponse.Height.Should().Be(imageProcessorResponse.Height);
        convertedResponse.Width.Should().Be(imageProcessorResponse.Width);
    }
    
    [Fact]
    public async Task GenerateDerivatives_ReturnsErrorAppetiserResponse_WhenNotSuccess()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseErrorModel { Message = "Error Message", Status = "Error" };

        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);

        var httpResponse =
            httpHandler.GetJsonResponseMessage(imageProcessorResponse, HttpStatusCode.UnprocessableContent);
        httpHandler.SetResponse(httpResponse);

        // Act
        var response =
            await sut.GenerateDerivatives(context, assetId, thumbnailSizes, ImageProcessorOperations.Derivative);

        // Assert
        var convertedResponse = (AppetiserResponseErrorModel)response;
        convertedResponse.Message.Should().Be(imageProcessorResponse.Message);
        convertedResponse.Status.Should().Be(imageProcessorResponse.Status);
    }
}
