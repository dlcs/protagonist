using System.Net;
using System.Net.Http.Headers;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Policies;
using Engine.Ingest;
using Engine.Ingest.Image;
using Engine.Ingest.Image.Appetiser;
using Engine.Ingest.Persistence;
using Engine.Settings;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Test.Helpers.Http;
using Test.Helpers.Settings;
using Test.Helpers.Storage;

namespace Engine.Tests.Ingest.Image.Appetiser;

public class AppetiserClientTests
{
    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly TestBucketWriter bucketWriter;
    private readonly IThumbCreator thumbnailCreator;
    private readonly EngineSettings engineSettings;
    private readonly AppetiserClient sut;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IFileSystem fileSystem;

    public AppetiserClientTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        fileSystem = A.Fake<IFileSystem>();
        bucketWriter = new TestBucketWriter("appetiser-test");
        engineSettings = new EngineSettings
        {
            ImageIngest = new ImageIngestSettings
            {
                ScratchRoot = "scratch/",
                DestinationTemplate = "dest/",
                SourceTemplate = "source/",
                ThumbsTemplate = "thumb/"
            }
        };
        thumbnailCreator = A.Fake<IThumbCreator>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        A.CallTo(() => storageKeyGenerator.GetStorageLocation(A<AssetId>._))
            .ReturnsLazily((AssetId assetId) =>
                new RegionalisedObjectInBucket("appetiser-test", assetId.ToString(), "Fake-Region"));

        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(engineSettings);

        var httpClient = new HttpClient(httpHandler);
        httpClient.BaseAddress = new Uri("http://image-processor/");
        sut = new AppetiserClient(httpClient, bucketWriter, storageKeyGenerator, thumbnailCreator, fileSystem,
            optionsMonitor, new NullLogger<AppetiserClient>());
    }
    
    [Fact]
    public async Task ProcessImage_CreatesRequiredDirectories()
    {
        // Arrange
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var context = GetIngestionContext();

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => fileSystem.CreateDirectory(A<string>._)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task ProcessImage_False_IfImageProcessorCallFails()
    {
        // Arrange
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var context = GetIngestionContext();

        // Act
        var result = await sut.ProcessImage(context);

        // Assert
        httpHandler.CallsMade.Should().ContainSingle(s => s == "http://image-processor/convert");
        result.Should().BeFalse();
        context.Asset.Should().NotBeNull();
    }

    [Theory]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    public async Task ProcessImage_SetsOperation_DerivatesOnly_IfJp2(string contentType)
    {
        // Arrange
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var context = GetIngestionContext(contentType: contentType);
        context.AssetFromOrigin.Location = "/file/on/disk";

        AppetiserRequestModel requestModel = null;
        httpHandler.RegisterCallback(async message =>
        {
            requestModel = await message.Content.ReadAsAsync<AppetiserRequestModel>();
        });

        // Act
        await sut.ProcessImage(context);

        // Assert
        httpHandler.CallsMade.Should().ContainSingle(s => s == "http://image-processor/convert");
        requestModel.Operation.Should().Be("derivatives-only");
    }

    [Fact]
    public async Task ProcessImage_SetsOperation_Ingest_IfNotJp2()
    {
        // Arrange
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var context = GetIngestionContext();
        AppetiserRequestModel requestModel = null;
        httpHandler.RegisterCallback(async message =>
        {
            requestModel = await message.Content.ReadAsAsync<AppetiserRequestModel>();
        });

        // Act
        await sut.ProcessImage(context);

        // Assert
        httpHandler.CallsMade.Should().ContainSingle(s => s == "http://image-processor/convert");
        requestModel.Operation.Should().Be("ingest");
    }

    [Fact]
    public async Task ProcessImage_UpdatesAssetSize()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
            Thumbs = Array.Empty<ImageOnDisk>()
        };

        var response = httpHandler.GetResponseMessage(JsonConvert.SerializeObject(imageProcessorResponse),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);

        var context = GetIngestionContext();

        // Act
        await sut.ProcessImage(context);

        // Assert
        context.Asset.Height.Should().Be(imageProcessorResponse.Height);
        context.Asset.Width.Should().Be(imageProcessorResponse.Width);
    }

    [Theory]
    [InlineData(true, OriginStrategyType.Default)]
    [InlineData(true, OriginStrategyType.BasicHttp)]
    [InlineData(true, OriginStrategyType.SFTP)]
    [InlineData(false, OriginStrategyType.S3Ambient)]
    public async Task ProcessImage_UploadsFileToBucket_AndSetsImageLocation_IfNotS3OptimisedStrategy(bool optimised,
        OriginStrategyType strategy)
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel { Thumbs = Array.Empty<ImageOnDisk>() };

        var response = httpHandler.GetResponseMessage(JsonConvert.SerializeObject(imageProcessorResponse),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);

        var context = GetIngestionContext("/1/2/test");
        context.AssetFromOrigin.CustomerOriginStrategy = new CustomerOriginStrategy
        {
            Optimised = optimised,
            Strategy = strategy
        };

        const string expected = "s3://appetiser-test/1/2/test";
        A.CallTo(() => storageKeyGenerator.GetS3Uri(A<ObjectInBucket>._, A<bool>._))
            .Returns(new Uri(expected));
        
        // Act
        await sut.ProcessImage(context);

        // Assert
        bucketWriter.ShouldHaveKey("1/2/test").WithFilePath("dest/test.jp2");
        context.ImageLocation.S3.Should().Be(expected);
    }

    [Fact]
    public async Task ProcessImage_UploadsFileToBucket_UsingLocationOnDisk_IfJp2()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel { Thumbs = Array.Empty<ImageOnDisk>() };

        var response = httpHandler.GetResponseMessage(JsonConvert.SerializeObject(imageProcessorResponse),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);

        const string locationOnDisk = "/file/on/disk";
        var context = GetIngestionContext("/1/2/test", "image/jp2");
        context.AssetFromOrigin.Location = locationOnDisk;
        context.AssetFromOrigin.CustomerOriginStrategy = new CustomerOriginStrategy
        {
            Strategy = OriginStrategyType.Default
        };

        // Act
        await sut.ProcessImage(context);

        // Assert
        bucketWriter.ShouldHaveKey("1/2/test").WithFilePath(locationOnDisk);
    }

    [Fact]
    public async Task ProcessImage_SetsImageLocation_WithoutUploading_IfNotS3OptimisedStrategy()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel { Thumbs = Array.Empty<ImageOnDisk>() };

        var response = httpHandler.GetResponseMessage(JsonConvert.SerializeObject(imageProcessorResponse),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);

        var context = GetIngestionContext();
        context.Asset.Origin = "https://s3.amazonaws.com/dlcs-storage/2/1/foo-bar";
        context.Asset.InitialOrigin = "https://s3.amazonaws.com/dlcs-storage-ignored/2/1/foo-bar";
        context.AssetFromOrigin.CustomerOriginStrategy = new CustomerOriginStrategy
        {
            Optimised = true,
            Strategy = OriginStrategyType.S3Ambient
        };
        const string expected = "s3://dlcs-storage/2/1/foo-bar";

        A.CallTo(() => storageKeyGenerator.GetS3Uri(A<ObjectInBucket>._, A<bool>._))
            .Returns(new Uri(expected));

        // Act
        await sut.ProcessImage(context);

        // Assert
        bucketWriter.ShouldNotHaveKey("0/0/something");
        context.ImageLocation.S3.Should().Be(expected);
    }

    [Fact]
    public async Task ProcessImage_ProcessesNewThumbs()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Thumbs = new[]
            {
                new ImageOnDisk { Height = 100, Width = 50, Path = "/path/to/thumb/100.jpg" },
            },
        };

        var response = httpHandler.GetResponseMessage(JsonConvert.SerializeObject(imageProcessorResponse),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);

        var context = GetIngestionContext();
        context.AssetFromOrigin.CustomerOriginStrategy = new CustomerOriginStrategy { Optimised = false };

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => thumbnailCreator.CreateNewThumbs(A<Asset>.That.Matches(a => a.Id == context.Asset.Id),
                A<IReadOnlyList<ImageOnDisk>>.That.Matches(t => t.Single().Path.EndsWith("100.jpg"))
            ))
            .MustHaveHappened();
    }

    [Fact]
    public async Task ProcessImage_ReturnsImageStorageObject()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel { Thumbs = Array.Empty<ImageOnDisk>() };

        var response = httpHandler.GetResponseMessage(JsonConvert.SerializeObject(imageProcessorResponse),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);

        var context = GetIngestionContext();
        context.AssetFromOrigin.CustomerOriginStrategy = new CustomerOriginStrategy { Optimised = false };

        A.CallTo(() => fileSystem.GetFileSize(A<string>._)).Returns(123L);

        // Act
        await sut.ProcessImage(context);

        // Assert
        var storage = context.ImageStorage;
        storage.Id.Should().Be("/1/2/something");
        storage.Customer.Should().Be(1);
        storage.Space.Should().Be(2);
        storage.LastChecked.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(30));
        storage.Size.Should().Be(123);
    }

    private static IngestionContext GetIngestionContext(string assetId = "/1/2/something",
        string contentType = "image/jpg")
    {
        var asset = new Asset { Id = assetId, Customer = 1, Space = 2 };
        asset
            .WithImageOptimisationPolicy(new ImageOptimisationPolicy { TechnicalDetails = Array.Empty<string>() })
            .WithThumbnailPolicy(new ThumbnailPolicy());

        var context = new IngestionContext(asset);
        return context.WithAssetFromOrigin(new AssetFromOrigin(asset.GetAssetId(), 123, "./scratch/here.jpg",
            contentType));
    }
}