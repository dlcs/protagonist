using System.Net;
using DLCS.Core.Exceptions;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Engine.Ingest.Image.ImageServer.Clients;
using Engine.Ingest.Image.ImageServer.Manipulation;
using Engine.Settings;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Http;
using Test.Helpers.Settings;

namespace Engine.Tests.Ingest.Image.ImageServer.Clients;

public class CantaloupeThumbsClientTests
{
    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly CantaloupeThumbsClient sut;

    private readonly List<string> defaultThumbs = new()
    {
        "!1024,1024"
    };

    private static readonly string ThumbsRoot = $"{Path.DirectorySeparatorChar}thumbs";

    public CantaloupeThumbsClientTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        var fileSystem = A.Fake<IFileSystem>();
        var imageManipulator = A.Fake<IImageManipulator>();

        var httpClient = new HttpClient(httpHandler);
        httpClient.BaseAddress = new Uri("http://image-processor/");
        sut = new CantaloupeThumbsClient(httpClient, fileSystem, imageManipulator, new NullLogger<CantaloupeThumbsClient>());
    }
    
    [Fact]
    public async Task GenerateThumbnails_ReturnsSuccessfulResponse_WhenOk()
    {
        // Arrange
        var assetId = new AssetId(2, 1, nameof(GenerateThumbnails_ReturnsSuccessfulResponse_WhenOk));
        var context = IngestionContextFactory.GetIngestionContext(assetId: assetId.ToString());
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

        context.WithLocation(new ImageLocation()
        {
            S3 = "//some/location/with/s3"
        });
        
        // Act
        var thumbs = await sut.GenerateThumbnails(context, defaultThumbs, ThumbsRoot);

        // Assert
        thumbs.Should().HaveCount(1);
        thumbs[0].Path.Should().Be($"{Path.DirectorySeparatorChar}thumbs{Path.DirectorySeparatorChar}thumb1");
    }
    
    [Fact]
    public async Task GenerateThumbnails_ThrowsException_WhenNotOk()
    {
        // Arrange
        var assetId = new AssetId(2, 1, nameof(GenerateThumbnails_ThrowsException_WhenNotOk));
        var context = IngestionContextFactory.GetIngestionContext(assetId: assetId.ToString());
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        context.WithLocation(new ImageLocation()
        {
            S3 = "//some/location/with/s3"
        });

        // Act
        Func<Task> action = async () => await sut.GenerateThumbnails(context, defaultThumbs, ThumbsRoot);
        
        // Assert
        await action.Should().ThrowAsync<HttpException>();
    }
    
    [Fact]
    public async Task GenerateThumbnails_ReturnsNothing_WhenCantaloupeReturns400()
    {
        // Arrange
        var assetId = new AssetId(2, 1, nameof(GenerateThumbnails_ReturnsNothing_WhenCantaloupeReturns400));
        var context = IngestionContextFactory.GetIngestionContext(assetId: assetId.ToString());
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.BadRequest));

        context.WithLocation(new ImageLocation()
        {
            S3 = "//some/location/with/s3"
        });

        // Act
        var thumbs = await sut.GenerateThumbnails(context, defaultThumbs, ThumbsRoot);

        // Assert
        thumbs.Should().HaveCount(0);
    }
    
    [Fact]
    public async Task GenerateThumbnails_Ignores400_AndProcessesRest()
    {
        // Arrange
        var assetId = new AssetId(2, 1, nameof(GenerateThumbnails_Ignores400_AndProcessesRest));
        var context = IngestionContextFactory.GetIngestionContext(assetId: assetId.ToString());

        var thumbSizes = new List<string> { "!1024,1024", "!400,400" };
        
        // first size returns BadRequest (400), then OK (200) after
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.BadRequest));
        httpHandler.RegisterCallback(_ => httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)));

        context.WithLocation(new ImageLocation()
        {
            S3 = "//some/location/with/s3"
        });

        // Act
        var thumbs = await sut.GenerateThumbnails(context, thumbSizes, ThumbsRoot);

        // Assert
        thumbs.Should().HaveCount(1);
        thumbs[0].Path.Should().Be($"{Path.DirectorySeparatorChar}thumbs{Path.DirectorySeparatorChar}thumb2");
    }
}