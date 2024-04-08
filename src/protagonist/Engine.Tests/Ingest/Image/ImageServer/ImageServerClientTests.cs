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
using Engine.Ingest.Image.ImageServer;
using Engine.Ingest.Image.ImageServer.Clients;
using Engine.Ingest.Image.ImageServer.Models;
using Engine.Settings;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Settings;
using Test.Helpers.Storage;

namespace Engine.Tests.Ingest.Image.ImageServer;

public class ImageServerClientTests
{
    private readonly TestBucketWriter bucketWriter;
    private readonly IThumbCreator thumbnailCreator;
    private readonly IAppetiserClient appetiserClient;
    private readonly ICantaloupeThumbsClient cantaloupeThumbsClient;
    private readonly EngineSettings engineSettings;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly ImageServerClient sut;
    private readonly IFileSystem fileSystem;
    private static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.Web);

    public ImageServerClientTests()
    {
        fileSystem = A.Fake<IFileSystem>();
        bucketWriter = new TestBucketWriter("appetiser-test");
        appetiserClient = A.Fake<IAppetiserClient>();
        cantaloupeThumbsClient = A.Fake<ICantaloupeThumbsClient>();
        engineSettings = new EngineSettings
        {
            ImageIngest = new ImageIngestSettings
            {
                ScratchRoot = "scratch/",
                DestinationTemplate ="{root}{customer}/{space}/{image}/output",
                SourceTemplate = "source/",
                ThumbsTemplate = "thumb/"
            }
        };
        thumbnailCreator = A.Fake<IThumbCreator>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        A.CallTo(() => storageKeyGenerator.GetStorageLocation(A<AssetId>._))
            .ReturnsLazily((AssetId assetId) =>
                new RegionalisedObjectInBucket("appetiser-test", assetId.ToString(), "Fake-Region"));
        A.CallTo(() => storageKeyGenerator.GetStoredOriginalLocation(A<AssetId>._))
            .ReturnsLazily((AssetId assetId) =>
                new RegionalisedObjectInBucket("appetiser-test", $"{assetId}/original", "Fake-Region"));
        A.CallTo(() => storageKeyGenerator.GetTransientImageLocation(A<AssetId>._))
            .ReturnsLazily((AssetId assetId) =>
                new RegionalisedObjectInBucket("appetiser-test", $"transient/{assetId.ToString()}", "Fake-Region"));

        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(engineSettings);
        
        sut = new ImageServerClient(appetiserClient, cantaloupeThumbsClient, bucketWriter, storageKeyGenerator, thumbnailCreator, fileSystem,
            optionsMonitor, new NullLogger<ImageServerClient>());
    }
    
    [Fact]
    public async Task ProcessImage_CreatesAndRemovesRequiredDirectories()
    {
        // Arrange
        var context = IngestionContextFactory.GetIngestionContext();

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => fileSystem.CreateDirectory(A<string>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => fileSystem.DeleteDirectory(A<string>._, true, true)).MustHaveHappenedTwiceExactly();
    }
    
    [Fact]
    public async Task ProcessImage_ChangesFileSavedLocationBasedOnImageIdWithBrackets()
    {
        // Arrange
        var context = IngestionContextFactory.GetIngestionContext(assetId: "1/2/some(id)");

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => fileSystem.CreateDirectory( "scratch/1/2/some_id_/output")).MustHaveHappenedOnceExactly();
        A.CallTo(() => fileSystem.DeleteDirectory(A<string>._, true, true)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task ProcessImage_False_IfImageProcessorCallFails()
    {
        // Arrange
        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new AppetiserResponseErrorModel()
            {
                Message = "error",
                Status = "some status"
            } as IAppetiserResponse));
        
        var context = IngestionContextFactory.GetIngestionContext();

        // Act
        var result = await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .MustHaveHappened();
        result.Should().BeFalse();
        context.Asset.Should().NotBeNull();
        context.Asset.Error.Should().Be("Appetiser Error: error");
    }

    [Theory]
    [InlineData("image/jp2", "default")]
    [InlineData("image/jpx", "default")]
    [InlineData("image/jpeg", "use-original")]
    public async Task ProcessImage_SetsOperation_Ingest_IfNotJp2AndUseOriginal(string contentType, string policy)
    {
        // Arrange
        var context = IngestionContextFactory.GetIngestionContext(contentType: contentType, imageDeliveryChannelPolicy: policy);
        
        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new AppetiserResponseModel()
            {
                Height = 100,
                Width = 100
            } as IAppetiserResponse));

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .MustHaveHappened();
        A.CallTo(() => cantaloupeThumbsClient.GenerateThumbnails(A<IngestionContext>._, A<List<string>>._, A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task ProcessImage_UpdatesAssetDimensions()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
        };

        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .Returns(Task.FromResult(imageProcessorResponse as IAppetiserResponse));

        var context = IngestionContextFactory.GetIngestionContext();

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
    public async Task ProcessImage_UploadsGeneratedFileToDlcsBucket_AndSetsImageLocation_IfNotS3OptimisedStrategy(bool optimised,
        OriginStrategyType strategy)
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel();

        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .Returns(Task.FromResult(imageProcessorResponse as IAppetiserResponse));
        A.CallTo(() => appetiserClient.GetJP2FilePath(A<AssetId>._, A<bool>._))
            .Returns("scratch/1/2/test/outputtest.jp2");

        var context = IngestionContextFactory.GetIngestionContext("/1/2/test");
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
        bucketWriter
            .ShouldHaveKey("1/2/test")
            .WithFilePath("scratch/1/2/test/outputtest.jp2")
            .WithContentType("image/jp2");
        context.ImageLocation.S3.Should().Be(expected);
        context.StoredObjects.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessImage_UploadsFileToBucket_UsingLocationOnDisk_IfUseOriginal_AndNotOptimised()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel();

        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .Returns(Task.FromResult(imageProcessorResponse as IAppetiserResponse));

        const string locationOnDisk = "/file/on/disk";
        var context = IngestionContextFactory.GetIngestionContext("/1/2/test", imageDeliveryChannelPolicy: "use-original");
        context.AssetFromOrigin.Location = locationOnDisk;

        // Act
        await sut.ProcessImage(context);

        // Assert
        bucketWriter
            .ShouldHaveKey("1/2/test/original")
            .WithFilePath(locationOnDisk)
            .WithContentType("image/jpg");
        context.StoredObjects.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessImage_SetsImageLocation_WithoutUploading_IfNotS3OptimisedStrategy()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel();

        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .Returns(Task.FromResult(imageProcessorResponse as IAppetiserResponse));

        var context = IngestionContextFactory.GetIngestionContext(imageDeliveryChannelPolicy: "use-original", optimised: true);
        context.Asset.Origin = "https://s3.amazonaws.com/dlcs-storage/2/1/foo-bar";

        const string expected = "s3://dlcs-storage/2/1/foo-bar";

        A.CallTo(() => storageKeyGenerator.GetS3Uri(A<ObjectInBucket>._, A<bool>._))
            .Returns(new Uri(expected));

        // Act
        await sut.ProcessImage(context);

        // Assert
        bucketWriter.Operations.Should().BeEmpty();
        context.ImageLocation.S3.Should().Be(expected);
        context.StoredObjects.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessImage_ProcessesNewThumbs()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel();
        
        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .Returns(Task.FromResult(imageProcessorResponse as IAppetiserResponse));
        
        A.CallTo(() => cantaloupeThumbsClient.GenerateThumbnails(
                A<IngestionContext>._, 
                A<List<string>>._, 
                A<CancellationToken>._))
            .Returns(Task.FromResult(new List<ImageOnDisk>()
            {
                new()
                {
                    Height = 100, 
                    Width = 50, 
                    Path = "/path/to/thumb/100.jpg"
                }
            }));

        var context = IngestionContextFactory.GetIngestionContext();
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
    public async Task ProcessImage_UseOriginal_NoImageDeliveryChannel(string originContentType)
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
        };

        const string expected = "s3://dlcs-storage/2/1/foo-bar";
        
        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .Returns(Task.FromResult(imageProcessorResponse as IAppetiserResponse));
        
        A.CallTo(() => cantaloupeThumbsClient.GenerateThumbnails(
                A<IngestionContext>._, 
                A<List<string>>._, 
                A<CancellationToken>._))
            .Returns(Task.FromResult(new List<ImageOnDisk>()
            {
                new()
                {
                    Path = "foo"
                },
                new()
                {
                    Path = "bar"
                }
            }));

        var context = IngestionContextFactory.GetIngestionContext(contentType: originContentType,
            cos: new CustomerOriginStrategy { Optimised = true, Strategy = OriginStrategyType.S3Ambient },
            imageDeliveryChannelPolicy: "use-original");
        
        context.Asset.ImageDeliveryChannels = new List<ImageDeliveryChannel>
        {
            new()
            { 
                Channel = AssetDeliveryChannels.Thumbnails,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault,
                DeliveryChannelPolicy = new DeliveryChannelPolicy()
                {
                    PolicyData = "[\"1000,1000\",\"400,400\",\"200,200\",\"100,100\"]"
                }
            }
        };
            
        context.AssetFromOrigin.Location = "/file/on/disk";
        context.Asset.Origin = "s3://origin/2/1/foo-bar";
        
        A.CallTo(() => fileSystem.GetFileSize(A<string>._)).Returns(100);

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .MustHaveHappened();
        A.CallTo(() => cantaloupeThumbsClient.GenerateThumbnails(A<IngestionContext>._, A<List<string>>._, A<CancellationToken>._))
            .MustHaveHappened();
        A.CallTo(() => thumbnailCreator.CreateNewThumbs(context.Asset, A<IReadOnlyList<ImageOnDisk>>._))
            .MustHaveHappened();
        context.ImageStorage.ThumbnailSize.Should().Be(200, "Thumbs saved");
        context.ImageStorage.Size.Should().Be(0, "JP2 not written");
        bucketWriter.Operations.Should().BeEmpty();
        context.Asset.Height.Should().Be(1000);
        context.Asset.Width.Should().Be(5000);
        context.StoredObjects.Should().BeEmpty();
    }
    
    [Theory]
    [InlineData("image/jp2", true, OriginStrategyType.BasicHttp)]
    [InlineData("image/jp2", false, OriginStrategyType.S3Ambient)]
    [InlineData("image/tiff", true, OriginStrategyType.S3Ambient)]
    public async Task ProcessImage_NotJp2_OptimisedOrigin(string originContentType, bool optimised,
        OriginStrategyType strategyType)
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
        };
        
        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .Returns(Task.FromResult(imageProcessorResponse as IAppetiserResponse));
        
        A.CallTo(() => cantaloupeThumbsClient.GenerateThumbnails(
                A<IngestionContext>._, 
                A<List<string>>._, 
                A<CancellationToken>._))
            .Returns(Task.FromResult(new List<ImageOnDisk>()
            {
                new()
                {
                    Path = "foo"
                },
                new()
                {
                    Path = "bar"
                }
            }));

        var context = IngestionContextFactory.GetIngestionContext(contentType: originContentType,
            cos: new CustomerOriginStrategy { Optimised = optimised, Strategy = strategyType });
        context.AssetFromOrigin.Location = "/file/on/disk";
        context.Asset.Origin = "s3://origin/2/1/foo-bar";
        A.CallTo(() => fileSystem.GetFileSize(A<string>._)).Returns(100);

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .MustHaveHappened();
        A.CallTo(() => cantaloupeThumbsClient.GenerateThumbnails(A<IngestionContext>._, A<List<string>>._, A<CancellationToken>._))
            .MustHaveHappened();
        A.CallTo(() => thumbnailCreator.CreateNewThumbs(context.Asset, A<IReadOnlyList<ImageOnDisk>>._))
            .MustHaveHappened();
        context.ImageStorage.ThumbnailSize.Should().Be(200, "Thumbs saved");
        context.ImageStorage.Size.Should().Be(100, "JP2 written to S3");
        bucketWriter.ShouldHaveKey("1/2/something");
        context.Asset.Height.Should().Be(imageProcessorResponse.Height, "JP2 Generated");
        context.Asset.Width.Should().Be(imageProcessorResponse.Width, "JP2 Generated");
        context.StoredObjects.Should().NotBeEmpty();
    }
    
    [Fact]
    public async Task ProcessImage_UseOriginal_AlreadyUploaded()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
        };
        
        A.CallTo(() => appetiserClient.GenerateJP2(A<IngestionContext>._, A<AssetId>._, A<CancellationToken>._))
            .Returns(Task.FromResult(imageProcessorResponse as IAppetiserResponse));
        
        A.CallTo(() => cantaloupeThumbsClient.GenerateThumbnails(
                A<IngestionContext>._, 
                A<List<string>>._, 
                A<CancellationToken>._))
            .Returns(Task.FromResult(new List<ImageOnDisk>()
            {
                new()
                {
                    Path = "foo"
                },
                new()
                {
                    Path = "bar"
                }
            }));

        var context = IngestionContextFactory.GetIngestionContext(
            cos: new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient },
            imageDeliveryChannelPolicy: "use-original");
        context.AssetFromOrigin.Location = "/file/on/disk";
        context.Asset.Origin = "s3://origin/2/1/foo-bar";
        var alreadyUploadedFile = new RegionalisedObjectInBucket("appetiser-test", $"{context.Asset.Id}/original", "Fake-Region");
        context.StoredObjects.Add(alreadyUploadedFile, -999);

        AppetiserRequestModel? requestModel = null;
        A.CallTo(() => fileSystem.GetFileSize(A<string>._)).Returns(100);

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => thumbnailCreator.CreateNewThumbs(context.Asset, A<IReadOnlyList<ImageOnDisk>>._))
            .MustHaveHappened();
        context.ImageStorage.ThumbnailSize.Should().Be(200, "Thumbs saved");
        context.ImageStorage.Size.Should().Be(0, "JP2 not written to S3");
        bucketWriter.Operations.Should().BeEmpty("JP2 not written to S3");
        context.Asset.Height.Should().Be(imageProcessorResponse.Height);
        context.Asset.Width.Should().Be(imageProcessorResponse.Width);
        context.StoredObjects.Should().ContainKey(alreadyUploadedFile).WhoseValue.Should()
            .Be(-999, "Value should not have changed");
    }
}