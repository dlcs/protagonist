using System.Net;
using Engine.Ingest.Image;
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
                ScratchRoot = $"scratch{Path.DirectorySeparatorChar}",
                DestinationTemplate = $"{{root}}{{customer}}{{space}}{Path.DirectorySeparatorChar}{{image}}/output",
                SourceTemplate = $"source{Path.DirectorySeparatorChar}",
                ThumbsTemplate = $"thumb{Path.DirectorySeparatorChar}"
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
        errorResponse.Status.Should().Be(500);
        errorResponse.Message.Should().Be("You must specify an operation");
    }
    
    [Theory]
    [InlineData(ImageProcessorOperations.Thumbnails | ImageProcessorOperations.Derivative, "ingest")]
    [InlineData(ImageProcessorOperations.Thumbnails, "derivatives-only")]
    public async Task GenerateDerivatives_MakesCorrectRequest(ImageProcessorOperations operation, string passedValue)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(asset: "foo");
        var context = IngestionContextFactory.GetIngestionContext(assetId);

        HttpRequestMessage message = null!;
        AppetiserRequestModel requestModel = null!;
        httpHandler.RegisterCallback(r =>
        {
            message = r;
            requestModel = message.Content.ReadAsAsync<AppetiserRequestModel>().Result;
        });
        var expectedSizes = thumbnailSizes.Select(sz => sz.ToString());

        // Act
        await sut.GenerateDerivatives(context, assetId, thumbnailSizes, operation);

        // Assert
        httpHandler.CallsMade.Should().ContainSingle().Which.Should().Be("http://image-processor/convert");
        message.Method.Should().Be(HttpMethod.Post);
        requestModel.Operation.Should().Be(passedValue);
        requestModel.Destination.Should().EndWith("991/foo/output/foo.jp2");
        requestModel.Source.Should().Be("source/foo.jpg");
        requestModel.ThumbDir.Should().Be("thumb/");
        requestModel.ThumbIIIFSizes.Should().BeEquivalentTo(expectedSizes);
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
    public async Task GenerateDerivatives_ReturnsSuccessfulAppetiserResponse_WithRewrittenPaths_DerivativeAndThumbs()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
            JP2 = "/99/1/simple/output/file.jp2",
            Thumbs =
            [
                new ImageOnDisk { Height = 10, Width = 20, Path = "/path/output/thumb/20.jpg", },
                new ImageOnDisk { Height = 100, Width = 200, Path = "/path/output/thumb/200.jpg", }
            ]
        };
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);

        var httpResponse = httpHandler.GetJsonResponseMessage(imageProcessorResponse, HttpStatusCode.OK);
        httpHandler.SetResponse(httpResponse);

        // Act
        var response =
            await sut.GenerateDerivatives(context, assetId, thumbnailSizes, ImageProcessorOperations.Derivative);
        
        // Assert
        var convertedResponse = (AppetiserResponseModel)response;
        convertedResponse.JP2.Should().StartWith("scratch")
            .And.Subject.Should().EndWith($"{Path.DirectorySeparatorChar}file.jp2");
        convertedResponse.Thumbs.First().Path.Should().EndWith($"{Path.DirectorySeparatorChar}20.jpg");
        convertedResponse.Thumbs.Last().Path.Should().EndWith($"{Path.DirectorySeparatorChar}200.jpg");
    }
    
    [Fact]
    public async Task GenerateDerivatives_ReturnsSuccessfulAppetiserResponse_WithRewrittenPaths_ThumbsOnly()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
            Thumbs =
            [
                new ImageOnDisk { Height = 10, Width = 20, Path = "/path/output/thumb/20.jpg", },
                new ImageOnDisk { Height = 100, Width = 200, Path = "/path/output/thumb/200.jpg", }
            ]
        };
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);

        var httpResponse = httpHandler.GetJsonResponseMessage(imageProcessorResponse, HttpStatusCode.OK);
        httpHandler.SetResponse(httpResponse);

        // Act
        var response =
            await sut.GenerateDerivatives(context, assetId, thumbnailSizes, ImageProcessorOperations.Derivative);
        
        // Assert
        var convertedResponse = (AppetiserResponseModel)response;
        convertedResponse.JP2.Should().BeNull();
        convertedResponse.Thumbs.First().Path.Should().EndWith($"{Path.DirectorySeparatorChar}20.jpg");
        convertedResponse.Thumbs.Last().Path.Should().EndWith($"{Path.DirectorySeparatorChar}200.jpg");
    }
    
    public static IEnumerable<object[]> ValidationException = new List<object[]>
    {
        new object[]
        {
            "Empty Json",
            "{}",
            "Appetiser Validation error"
        },
        new object[]
        {
            "Single validation error",
            "{\"detail\":[{\"type\":\"path_not_file\",\"loc\":[\"body\",\"source\"],\"msg\":\"Path does not point to a fail\",\"input\":\"/path/to/file.jpg\"}]}",
            "source: /path/to/file.jpg - Path does not point to a fail"
        },
        new object[]
        {
            "Multiple validation error",
            "{\"detail\":[{\"type\":\"path_not_file\",\"loc\":[\"body\",\"source\"],\"msg\":\"Path does not point to a fail\",\"input\":\"/path/to/file.jpg\"},{\"type\":\"path_not_directory\",\"loc\":[\"body\",\"thumbDir\"],\"msg\":\"Path does not point to a directory\",\"input\":\"/output\"}]}",
            "source: /path/to/file.jpg - Path does not point to a fail. thumbDir: /output - Path does not point to a directory"
        },
        new object[]
        {
            "Single validation minimal",
            "{\"detail\":[{\"type\":\"path_not_file\",\"loc\":[\"source\"]}]}",
            "source:  - "
        },
    };

    [Theory]
    [MemberData(nameof(ValidationException))]
    public async Task GenerateDerivatives_ReturnsErrorAppetiserResponse_WhenValidationException(string reason,
        string json, string expected)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);

        var httpResponse = httpHandler.GetResponseMessage(json, HttpStatusCode.UnprocessableContent);
        httpHandler.SetResponse(httpResponse);

        // Act
        var response =
            await sut.GenerateDerivatives(context, assetId, thumbnailSizes, ImageProcessorOperations.Derivative);

        // Assert
        var convertedResponse = (AppetiserResponseErrorModel)response;
        convertedResponse.Message.Should().Be(expected, reason);
        convertedResponse.Status.Should().Be(422);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task GenerateDerivatives_ReturnsErrorAppetiserResponse_WhenNotSuccess(HttpStatusCode statusCode)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);

        var httpResponse = httpHandler.GetResponseMessage("{\"detail\":\"uh-oh\"}", statusCode);
        httpHandler.SetResponse(httpResponse);

        // Act
        var response =
            await sut.GenerateDerivatives(context, assetId, thumbnailSizes, ImageProcessorOperations.Derivative);

        // Assert
        var convertedResponse = (AppetiserResponseErrorModel)response;
        convertedResponse.Message.Should().Be("uh-oh");
        convertedResponse.Status.Should().Be((int)statusCode);
    }
    
    [Fact]
    public async Task GenerateDerivatives_ReturnsErrorAppetiserResponse_IfNoDetail()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);

        var httpResponse = httpHandler.GetResponseMessage("{\"foo\":\"bar\"}", HttpStatusCode.Conflict);
        httpHandler.SetResponse(httpResponse);

        // Act
        var response =
            await sut.GenerateDerivatives(context, assetId, thumbnailSizes, ImageProcessorOperations.Derivative);

        // Assert
        var convertedResponse = (AppetiserResponseErrorModel)response;
        convertedResponse.Message.Should().Be("Unknown response from Appetiser");
        convertedResponse.Status.Should().Be(409);
    }
}
