using System.Net;
using DLCS.Core.Exceptions;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Engine.Ingest.Image;
using Engine.Ingest.Image.ImageServer.Clients;
using Engine.Ingest.Image.ImageServer.Measuring;
using Engine.Settings;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Net.Http.Headers;
using Test.Helpers.Data;
using Test.Helpers.Http;
using Test.Helpers.Settings;
using CookieHeaderValue = System.Net.Http.Headers.CookieHeaderValue;

namespace Engine.Tests.Ingest.Image.ImageServer.Clients;

public class CantaloupeThumbsClientTests
{
    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly CantaloupeThumbsClient sut;
    private readonly IImageMeasurer imageMeasurer;
    private readonly HttpClient httpClient;

    private readonly List<string> defaultThumbs = new()
    {
        "!1024,1024"
    };

    private static readonly string ThumbsRoot = $"{Path.DirectorySeparatorChar}thumbs";

    public CantaloupeThumbsClientTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        var fileSystem = A.Fake<IFileSystem>();
        imageMeasurer = A.Fake<IImageMeasurer>();

        A.CallTo(() => imageMeasurer.MeasureImage(A<string>._, A<CancellationToken>._)).Returns(new ImageOnDisk());

        httpClient = new HttpClient(httpHandler);
        httpClient.BaseAddress = new Uri("http://image-processor/");
        
        var engineSettings = new EngineSettings
        {
            ImageIngest = new ImageIngestSettings()
        };
        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(engineSettings);
        
        
        sut = new CantaloupeThumbsClient(httpClient, fileSystem, imageMeasurer, optionsMonitor, new NullLogger<CantaloupeThumbsClient>());
    }
    
    [Fact]
    public async Task GenerateThumbnails_ReturnsThumbForSuccessfulResponse()
    {
        // Arrange
        var assetId = new AssetId(2, 1, nameof(GenerateThumbnails_ReturnsThumbForSuccessfulResponse));
        var context = IngestionContextFactory.GetIngestionContext(assetId: assetId.ToString());
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));
        context.Asset.Width = 2000;
        context.Asset.Height = 2000;

        context.WithLocation(new ImageLocation
        {
            S3 = "//some/location/with/s3"
        });
        
        // Act
        var thumbs = await sut.GenerateThumbnails(context, defaultThumbs, ThumbsRoot);

        // Assert
        thumbs.Should().HaveCount(1);
        thumbs[0].Height.Should().Be(1024);
        thumbs[0].Width.Should().Be(1024);
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
        context.Asset.Width = 200;
        context.Asset.Height = 200;

        var thumbSizes = new List<string> { "!1024,1024", "!400,400" };
        
        // first size returns BadRequest (400), then OK (200) after
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.BadRequest));
        httpHandler.RegisterCallback(_ => httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)));

        context.WithLocation(new ImageLocation
        {
            S3 = "//some/location/with/s3"
        });

        // Act
        var thumbs = await sut.GenerateThumbnails(context, thumbSizes, ThumbsRoot);

        // Assert
        thumbs.Should().HaveCount(1);
        thumbs[0].Height.Should().Be(200);
        thumbs[0].Width.Should().Be(200);
    }

    [Theory]
    [MemberData(nameof(ThumbsAndResults))]
    public async Task GenerateThumbnails_SizeHandling(Dictionary<string, ImageOnDiskResults> thumbsAndResult, int width,
        int height, string reason)
    {
        // Arrange
        var assetId = new AssetId(2, 1, nameof(GenerateThumbnails_SizeHandling));
        var context = IngestionContextFactory.GetIngestionContext(assetId: assetId.ToString());
        context.Asset.Width = width;
        context.Asset.Height = height;

        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));
        httpHandler.RegisterCallback(_ => httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)));

        var thumbSizes = thumbsAndResult.Keys.ToList();
        var expected = thumbsAndResult.Values.Select(v => v.Expected).ToList();
        var fromImageServer = thumbsAndResult.Values.Select(v => v.ReturnedFromImageServer).ToArray();

        A.CallTo(() => imageMeasurer.MeasureImage(A<string>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(fromImageServer);

        context.WithLocation(new ImageLocation
        {
            S3 = "//some/location/with/s3"
        });

        // Act
        var thumbs = await sut.GenerateThumbnails(context, thumbSizes, ThumbsRoot);

        // Assert
        thumbs.Should().BeEquivalentTo(expected, reason);
    }
    
    [Fact]
    public async Task GenerateThumbnails_InvalidOperationException_WhenMeasureImageReturnsNull()
    {
        // Arrange
        var assetId = new AssetId(2, 1, nameof(GenerateThumbnails_ReturnsThumbForSuccessfulResponse));
        var context = IngestionContextFactory.GetIngestionContext(assetId: assetId.ToString());
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));
        context.Asset.Width = 2000;
        context.Asset.Height = 2000;

        context.WithLocation(new ImageLocation
        {
            S3 = "//some/location/with/s3"
        });

        ImageOnDisk returnedFromImageMeasurer = null;

        A.CallTo(() => imageMeasurer.MeasureImage(A<string>._, A<CancellationToken>._))
            .Returns(returnedFromImageMeasurer);
        
        // Act
        Func<Task>  action = async () => await sut.GenerateThumbnails(context, defaultThumbs, ThumbsRoot);
        
        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }
    
    [Fact]
    public async Task GenerateThumbnails_ReturnsThumbForSuccessfulResponse_AfterFirstImageMeasurerFailure()
    {
        // Arrange
        var assetId = new AssetId(2, 1, nameof(GenerateThumbnails_ReturnsThumbForSuccessfulResponse));
        var context = IngestionContextFactory.GetIngestionContext(assetId: assetId.ToString());
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));
        context.Asset.Width = 2000;
        context.Asset.Height = 2000;

        context.WithLocation(new ImageLocation
        {
            S3 = "//some/location/with/s3"
        });
        
        ImageOnDisk returnedFromImageMeasurer = null;

        A.CallTo(() => imageMeasurer.MeasureImage(A<string>._, A<CancellationToken>._))
            .Returns(returnedFromImageMeasurer).Once().Then.Returns(new ImageOnDisk());
        
        // Act
        var thumbs = await sut.GenerateThumbnails(context, defaultThumbs, ThumbsRoot);

        // Assert
        thumbs.Should().HaveCount(1);
        thumbs[0].Height.Should().Be(1024);
        thumbs[0].Width.Should().Be(1024);
    }

    public static IEnumerable<object[]> ThumbsAndResults => new List<object[]>
    {
        new object[]
        {
            new Dictionary<string, ImageOnDiskResults>
            {
                ["!200,200"] = new(new ImageOnDisk { Width = 100, Height = 200 }), // matching
                ["!250,250"] = new(new ImageOnDisk { Width = 124, Height = 250 }), // down by one on shortest edge 
                ["!100,100"] = new(new ImageOnDisk { Width = 51, Height = 100 }), // up by one on shortest edge
                ["200,"] = new(new ImageOnDisk { Width = 200, Height = 400 }), // matching
                ["250,"] = new(new ImageOnDisk { Width = 250, Height = 499 }), // down by one on non-confined dimension 
                ["100,"] = new(new ImageOnDisk { Width = 100, Height = 201 }), // up by one on non-confined dimension
                [",200"] = new(new ImageOnDisk { Width = 100, Height = 200 }), // matching
                [",250"] = new(new ImageOnDisk { Width = 124, Height = 250 }), // down by one on non-confined dimension 
                [",100"] = new(new ImageOnDisk { Width = 51, Height = 100 }), // up by one on non-confined dimension
            },
            1000,
            2000,
            "Portrait images - valid sizes untouched",
        },
        new object[]
        {
            new Dictionary<string, ImageOnDiskResults>
            {
                ["!250,250"] = new(new ImageOnDisk { Width = 125, Height = 249 },
                    new() { Width = 125, Height = 250 }), // down by one on shortest edge 
                ["!100,100"] = new(new ImageOnDisk { Width = 50, Height = 101 },
                    new() { Width = 50, Height = 100 }), // up by one on longest edge
                ["250,"] = new(new ImageOnDisk { Width = 249, Height = 500 },
                    new() { Width = 250, Height = 500 }), // down by one on confined dimension 
                ["100,"] = new(new ImageOnDisk { Width = 101, Height = 200 },
                    new() { Width = 100, Height = 200 }), // up by one on confined dimension
                [",250"] = new(new ImageOnDisk { Width = 125, Height = 251 },
                    new() { Width = 125, Height = 250 }), // down by one on confined dimension 
                [",100"] = new(new ImageOnDisk { Width = 50, Height = 101 },
                    new() { Width = 50, Height = 100 }), // up by one on confined dimension
            },
            1000,
            2000,
            "Portrait images - invalid sizes altered",
        },
        new object[]
        {
            new Dictionary<string, ImageOnDiskResults>
            {
                ["!200,200"] = new(new ImageOnDisk { Width = 200, Height = 100 }), // matching
                ["!250,250"] = new(new ImageOnDisk { Width = 250, Height = 124 }), // down by one on shortest edge 
                ["!100,100"] = new(new ImageOnDisk { Width = 100, Height = 51 }), // up by one on shortest edge
                ["200,"] = new(new ImageOnDisk { Width = 400, Height = 200 }), // matching
                ["250,"] = new(new ImageOnDisk { Width = 400, Height = 250 }), // down by one on non-confined dimension 
                ["100,"] = new(new ImageOnDisk { Width = 201, Height = 100 }), // up by one on non-confined dimension
                [",200"] = new(new ImageOnDisk { Width = 200, Height = 100 }), // matching
                [",250"] = new(new ImageOnDisk { Width = 250, Height = 124 }), // down by one on non-confined dimension 
                [",100"] = new(new ImageOnDisk { Width = 100, Height = 51 }), // up by one on non-confined dimension
            },
            2000,
            1000,
            "Landscape images - valid sizes untouched",
        },
        new object[]
        {
            new Dictionary<string, ImageOnDiskResults>
            {
                ["!250,250"] = new(new ImageOnDisk { Width = 249, Height = 125 },
                    new() { Width = 250, Height = 125 }), // down by one on shortest edge 
                ["250,"] = new(new ImageOnDisk { Width = 500, Height = 249 },
                    new() { Width = 500, Height = 250 }), // down by one on confined dimension 
                ["100,"] = new(new ImageOnDisk { Width = 200, Height = 101 },
                    new() { Width = 200, Height = 100 }), // up by one on confined dimension
                [",250"] = new(new ImageOnDisk { Width = 251, Height = 125 },
                    new() { Width = 250, Height = 125 }), // down by one on confined dimension 
                [",100"] = new(new ImageOnDisk { Width = 101, Height = 50 },
                    new() { Width = 100, Height = 50 }), // up by one on confined dimension
            },
            2000,
            1000,
            "Landscape images - invalid sizes altered",
        },
    };
    
    [Fact]
    public async Task GenerateThumbnails_UpdatesHandlerWithCookies()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId: assetId.ToString());

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add(HeaderNames.SetCookie, new List<string?>()
        {
            "AWSALB=_remove_; Path=/",
            "AWSALBCORS=_remove_; Path=/"
        });
        httpHandler.SetResponse(response);
        List<CookieHeaderValue> cookieHeaders = new();

        context.Asset.Width = 2000;
        context.Asset.Height = 2000;

        context.WithLocation(new ImageLocation
        {
            S3 = "//some/location/with/s3"
        });
        
        await sut.GenerateThumbnails(context, defaultThumbs, ThumbsRoot);
        
        httpHandler.RegisterCallback(message => cookieHeaders = message.Headers.GetCookies().ToList());
        httpHandler.GetResponseMessage("{ \"engine\": \"hello\" }", HttpStatusCode.OK);
    
        // Act
        await sut.GenerateThumbnails(context, defaultThumbs, ThumbsRoot);

        // Assert
        cookieHeaders.Count.Should().Be(2);
        cookieHeaders[0].Cookies[0].Name.Should().Be("AWSALB");
        cookieHeaders[0].Cookies[0].Value.Should().Be("_remove_");
        cookieHeaders[1].Cookies[0].Name.Should().Be("AWSALBCORS");
    }
    
    [Fact]
    public async Task GenerateThumbnails_DoesNotUpdateHandlerWithCookiesWhenUnrecognised()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId: assetId.ToString());

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add(HeaderNames.SetCookie, new List<string?>()
        {
            "SOMECOOKIE=_remove_; Path=/",
            "SOMECOOKIE2=_remove_; Path=/"
        });
        httpHandler.SetResponse(response);
        List<CookieHeaderValue> cookieHeaders = new();

        context.Asset.Width = 2000;
        context.Asset.Height = 2000;

        context.WithLocation(new ImageLocation
        {
            S3 = "//some/location/with/s3"
        });
        
        await sut.GenerateThumbnails(context, defaultThumbs, ThumbsRoot);
        
        httpHandler.RegisterCallback(message => cookieHeaders = message.Headers.GetCookies().ToList());
        httpHandler.GetResponseMessage("{ \"engine\": \"hello\" }", HttpStatusCode.OK);
    
        // Act
        await sut.GenerateThumbnails(context, defaultThumbs, ThumbsRoot);

        // Assert
        cookieHeaders.Count.Should().Be(0);
    }

    [Fact]
    public async Task GenerateThumbnails_UpdatesHandlerWithNoCookiesSet()
    {
        // Arrange
        var assetId = new AssetId(2, 1, nameof(GenerateThumbnails_ReturnsThumbForSuccessfulResponse));
        var context = IngestionContextFactory.GetIngestionContext(assetId: assetId.ToString());

        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));
        context.Asset.Width = 2000;
        context.Asset.Height = 2000;

        context.WithLocation(new ImageLocation
        {
            S3 = "//some/location/with/s3"
        });
        
        await sut.GenerateThumbnails(context, defaultThumbs, ThumbsRoot);
        
        List<CookieHeaderValue> cookieHeaders = new();
        httpHandler.RegisterCallback(message => cookieHeaders = message.Headers.GetCookies().ToList());
        httpHandler.GetResponseMessage("{ \"engine\": \"hello\" }", HttpStatusCode.OK);
    
        // Act
        await sut.GenerateThumbnails(context, defaultThumbs, ThumbsRoot);
        
        // Assert
        cookieHeaders.Count.Should().Be(0);
    }

    public class ImageOnDiskResults
    {
        public ImageOnDisk ReturnedFromImageServer { get; }
        public ImageOnDisk Expected { get; }

        public ImageOnDiskResults(ImageOnDisk returnedFromImageServer, ImageOnDisk? expected = null)
        {
            ReturnedFromImageServer = returnedFromImageServer;
            Expected = expected ?? returnedFromImageServer;
        }
    }
}