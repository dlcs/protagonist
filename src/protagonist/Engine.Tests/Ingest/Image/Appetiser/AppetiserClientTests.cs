using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
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
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly AppetiserClient sut;
    private readonly IFileSystem fileSystem;
    private static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.Web);

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
    public async Task ProcessImage_CreatesAndRemovesRequiredDirectories()
    {
        // Arrange
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var context = GetIngestionContext();

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => fileSystem.CreateDirectory(A<string>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => fileSystem.DeleteDirectory(A<string>._, true, true)).MustHaveHappenedTwiceExactly();
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
    
    [Fact]
    public async Task ProcessImage_ThumbsChannelOnly()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
            Thumbs = new ImageOnDisk[] { new() { Path = "foo" }, new() { Path = "bar" } }
        };
        var response = httpHandler.GetResponseMessage(JsonSerializer.Serialize(imageProcessorResponse, Settings),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);
        
        var context = GetIngestionContext(deliveryChannel: new[] { AssetDeliveryChannels.Thumbs });
        context.AssetFromOrigin.Location = "/file/on/disk";

        AppetiserRequestModel? requestModel = null;
        httpHandler.RegisterCallback(async message =>
        {
            requestModel = await message.Content.ReadAsAsync<AppetiserRequestModel>();
        });
        A.CallTo(() => fileSystem.GetFileSize(A<string>._)).Returns(100);
    }

    [Theory]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    public async Task ProcessImage_SetsOperation_DerivatesOnly_IfJp2_AndUseOriginal(string contentType)
    {
        // Arrange
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var context = GetIngestionContext(contentType: contentType, imageOptimisationPolicy: "use-original");
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

    [Theory]
    [InlineData("image/jp2", "fastest")]
    [InlineData("image/jpx", "fastest")]
    [InlineData("image/jpeg", "use-original")]
    public async Task ProcessImage_SetsOperation_Ingest_IfNotJp2AndUseOriginal(string contentType, string iop)
    {
        // Arrange
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var context = GetIngestionContext(contentType: contentType, imageOptimisationPolicy: iop);
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

        var response = httpHandler.GetResponseMessage(JsonSerializer.Serialize(imageProcessorResponse, Settings),
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

    [Fact]
    public async Task ProcessImage_ImageChannelOnly()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
            Thumbs = new ImageOnDisk[] { new() { Path = "foo" }, new() { Path = "bar" } }
        };
        var response = httpHandler.GetResponseMessage(JsonSerializer.Serialize(imageProcessorResponse, Settings),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);
        
        var context = GetIngestionContext(deliveryChannel: new[] { AssetDeliveryChannels.Image });
        context.AssetFromOrigin.Location = "/file/on/disk";

        AppetiserRequestModel? requestModel = null;
        httpHandler.RegisterCallback(async message =>
        {
            requestModel = await message.Content.ReadAsAsync<AppetiserRequestModel>();
        });
        A.CallTo(() => fileSystem.GetFileSize(A<string>._)).Returns(100);

        // Act
        await sut.ProcessImage(context);

        // Assert
        requestModel.Operation.Should().Be("ingest");
        A.CallTo(() => thumbnailCreator.CreateNewThumbs(context.Asset, A<IReadOnlyList<ImageOnDisk>>._))
            .MustNotHaveHappened();
        context.ImageStorage.ThumbnailSize.Should().Be(0, "Thumbs not saved");
        context.ImageStorage.Size.Should().Be(100, "JP2 written to S3");
        context.Asset.Height.Should().Be(imageProcessorResponse.Height, "JP2 generated");
        context.Asset.Width.Should().Be(imageProcessorResponse.Width, "JP2 generated");
    }

    [Theory]
    [InlineData(true, OriginStrategyType.Default)]
    [InlineData(true, OriginStrategyType.BasicHttp)]
    [InlineData(true, OriginStrategyType.SFTP)]
    [InlineData(false, OriginStrategyType.S3Ambient)]
    public async Task ProcessImage_UploadsGeneratedFileToDlcsBucket_AndSetsImageLocation_IfNotS3OptimisedStrategy(bool optimised,
        OriginStrategyType strategy)
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel { Thumbs = Array.Empty<ImageOnDisk>() };

        var response = httpHandler.GetResponseMessage(JsonSerializer.Serialize(imageProcessorResponse, Settings),
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
        bucketWriter.ShouldHaveKey("1/2/test").WithFilePath("dest/test.jp2").WithContentType("image/jp2");
        context.ImageLocation.S3.Should().Be(expected);
    }

    [Fact]
    public async Task ProcessImage_UploadsFileToBucket_UsingLocationOnDisk_IfUseOriginal_AndNotOptimised()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel { Thumbs = Array.Empty<ImageOnDisk>() };

        var response = httpHandler.GetResponseMessage(JsonSerializer.Serialize(imageProcessorResponse, Settings),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);

        const string locationOnDisk = "/file/on/disk";
        var context = GetIngestionContext("/1/2/test", "image/jpg", imageOptimisationPolicy: "use-original");
        context.AssetFromOrigin.Location = locationOnDisk;

        // Act
        await sut.ProcessImage(context);

        // Assert
        bucketWriter.ShouldHaveKey("1/2/test").WithFilePath(locationOnDisk).WithContentType("image/jpg");
    }

    [Fact]
    public async Task ProcessImage_SetsImageLocation_WithoutUploading_IfNotS3OptimisedStrategy()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel { Thumbs = Array.Empty<ImageOnDisk>() };

        var response = httpHandler.GetResponseMessage(JsonSerializer.Serialize(imageProcessorResponse, Settings),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);

        var context = GetIngestionContext(imageOptimisationPolicy: "use-original", optimised: true);
        context.Asset.Origin = "https://s3.amazonaws.com/dlcs-storage/2/1/foo-bar";
        context.Asset.InitialOrigin = "https://s3.amazonaws.com/dlcs-storage-ignored/2/1/foo-bar";
        
        const string expected = "s3://dlcs-storage/2/1/foo-bar";

        A.CallTo(() => storageKeyGenerator.GetS3Uri(A<ObjectInBucket>._, A<bool>._))
            .Returns(new Uri(expected));

        // Act
        await sut.ProcessImage(context);

        // Assert
        bucketWriter.Operations.Should().BeEmpty();
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

        var response = httpHandler.GetResponseMessage(JsonSerializer.Serialize(imageProcessorResponse, Settings),
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
    
    [Theory]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    public async Task ProcessImage_BothChannels_JP2OptimisedOrigin(string originContentType)
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
            Thumbs = new ImageOnDisk[] { new() { Path = "foo" }, new() { Path = "bar" } }
        };
        var response = httpHandler.GetResponseMessage(JsonSerializer.Serialize(imageProcessorResponse, Settings),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);

        var context = GetIngestionContext(contentType: originContentType,
            cos: new CustomerOriginStrategy { Optimised = true, Strategy = OriginStrategyType.S3Ambient });
        context.AssetFromOrigin.Location = "/file/on/disk";
        context.Asset.Origin = "s3://origin/2/1/foo-bar";

        AppetiserRequestModel? requestModel = null;
        httpHandler.RegisterCallback(async message =>
        {
            requestModel = await message.Content.ReadAsAsync<AppetiserRequestModel>();
        });
        A.CallTo(() => fileSystem.GetFileSize(A<string>._)).Returns(100);

        // Act
        await sut.ProcessImage(context);

        // Assert
        requestModel.Operation.Should().Be("derivatives-only");
        A.CallTo(() => thumbnailCreator.CreateNewThumbs(context.Asset, A<IReadOnlyList<ImageOnDisk>>._))
            .MustHaveHappened();
        context.ImageStorage.ThumbnailSize.Should().Be(200, "Thumbs saved");
        context.ImageStorage.Size.Should().Be(0, "JP2 not written to S3");
        bucketWriter.Operations.Should().BeEmpty("JP2 not written to S3");
        context.Asset.Height.Should().Be(imageProcessorResponse.Height);
        context.Asset.Width.Should().Be(imageProcessorResponse.Width);
    }
    
    [Theory]
    [InlineData("image/jp2")]
    [InlineData("image/jpx")]
    public async Task ProcessImage_ImageChannelOnly_JP2OptimisedOrigin(string originContentType)
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
            Thumbs = new ImageOnDisk[] { new() { Path = "foo" }, new() { Path = "bar" } }
        };
        var response = httpHandler.GetResponseMessage(JsonSerializer.Serialize(imageProcessorResponse, Settings),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);

        var context = GetIngestionContext(contentType: originContentType,
            deliveryChannel: new[] { AssetDeliveryChannels.Image },
            cos: new CustomerOriginStrategy { Optimised = true, Strategy = OriginStrategyType.S3Ambient });
        context.AssetFromOrigin.Location = "/file/on/disk";
        context.Asset.Origin = "s3://origin/2/1/foo-bar";
        A.CallTo(() => fileSystem.GetFileSize(A<string>._)).Returns(100);

        // Act
        await sut.ProcessImage(context);

        // Assert
        httpHandler.CallsMade.Should().BeEmpty("Thumbs not required, origin already tile-optimised");
        A.CallTo(() => thumbnailCreator.CreateNewThumbs(context.Asset, A<IReadOnlyList<ImageOnDisk>>._))
            .MustNotHaveHappened();
        context.ImageStorage.ThumbnailSize.Should().Be(0, "Thumbs not saved");
        context.ImageStorage.Size.Should().Be(0, "JP2 not written to S3");
        bucketWriter.Operations.Should().BeEmpty("JP2 not written to S3");
        context.Asset.Height.Should().NotBe(imageProcessorResponse.Height, "JP2 not generated");
        context.Asset.Width.Should().NotBe(imageProcessorResponse.Width, "JP2 not generated");
    }

    [Theory]
    [InlineData("image/jp2", true, OriginStrategyType.BasicHttp)]
    [InlineData("image/jp2", false, OriginStrategyType.S3Ambient)]
    [InlineData("image/tiff", true, OriginStrategyType.S3Ambient)]
    public async Task ProcessImage_BothChannels_AndNotJp2OptimisedOrigin(string originContentType, bool optimised,
        OriginStrategyType strategyType)
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
            Thumbs = new ImageOnDisk[] { new() { Path = "foo" }, new() { Path = "bar" } }
        };
        var response = httpHandler.GetResponseMessage(JsonSerializer.Serialize(imageProcessorResponse, Settings),
            HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        httpHandler.SetResponse(response);

        var context = GetIngestionContext(contentType: originContentType,
            cos: new CustomerOriginStrategy { Optimised = optimised, Strategy = strategyType });
        context.AssetFromOrigin.Location = "/file/on/disk";
        context.Asset.Origin = "s3://origin/2/1/foo-bar";

        AppetiserRequestModel? requestModel = null;
        httpHandler.RegisterCallback(async message =>
        {
            requestModel = await message.Content.ReadAsAsync<AppetiserRequestModel>();
        });
        A.CallTo(() => fileSystem.GetFileSize(A<string>._)).Returns(100);

        // Act
        await sut.ProcessImage(context);

        // Assert
        requestModel.Operation.Should().Be("ingest");
        A.CallTo(() => thumbnailCreator.CreateNewThumbs(context.Asset, A<IReadOnlyList<ImageOnDisk>>._))
            .MustHaveHappened();
        context.ImageStorage.ThumbnailSize.Should().Be(200, "Thumbs saved");
        context.ImageStorage.Size.Should().Be(100, "JP2 written to S3");
        bucketWriter.ShouldHaveKey("1/2/something");
        context.Asset.Height.Should().Be(imageProcessorResponse.Height, "JP2 Generated");
        context.Asset.Width.Should().Be(imageProcessorResponse.Width, "JP2 Generated");
    }    

    private static IngestionContext GetIngestionContext(string assetId = "/1/2/something",
        string contentType = "image/jpg", string[]? deliveryChannel = null, CustomerOriginStrategy? cos = null,
        string imageOptimisationPolicy = "fast-high", bool optimised = false)
    {
        deliveryChannel ??= new[] { AssetDeliveryChannels.Image, AssetDeliveryChannels.Thumbs };
        cos ??= new CustomerOriginStrategy { Strategy = OriginStrategyType.Default, Optimised = optimised };
        var asset = new Asset
        {
            Id = AssetId.FromString(assetId), Customer = 1, Space = 2, DeliveryChannel = deliveryChannel, MediaType = contentType
        };

        asset
            .WithImageOptimisationPolicy(new ImageOptimisationPolicy
            {
                Id = imageOptimisationPolicy,
                TechnicalDetails = Array.Empty<string>()
            })
            .WithThumbnailPolicy(new ThumbnailPolicy());

        var context = new IngestionContext(asset);
        var assetFromOrigin = new AssetFromOrigin(asset.Id, 123, "./scratch/here.jpg", contentType)
        {
            CustomerOriginStrategy = cos
        };
        
        return context.WithAssetFromOrigin(assetFromOrigin);
    }
}