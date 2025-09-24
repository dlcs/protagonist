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
using IIIF.ImageApi;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Data;
using Test.Helpers.Settings;
using Test.Helpers.Storage;

namespace Engine.Tests.Ingest.Image.ImageServer;

public class AppetiserImageProcessorTests
{
    private readonly TestBucketWriter bucketWriter;
    private readonly IThumbCreator thumbnailCreator;
    private readonly IImageProcessorClient appetiserClient;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly AppetiserImageProcessor sut;
    private readonly IFileSystem fileSystem;

    public AppetiserImageProcessorTests()
    {
        fileSystem = A.Fake<IFileSystem>();
        bucketWriter = new TestBucketWriter("appetiser-test");
        appetiserClient = A.Fake<IImageProcessorClient>();
        var engineSettings = new EngineSettings
        {
            ImageIngest = new ImageIngestSettings
            {
                ScratchRoot = "scratch/",
                DestinationTemplate ="{root}{customer}/{space}/{image}/output",
                SourceTemplate = "source/",
                ThumbsTemplate = "thumb/",
                DefaultThumbs = ["!100,100", "!200,200", "!400,400", "!1024,1024"]
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

        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(engineSettings);

        sut = new AppetiserImageProcessor(appetiserClient, bucketWriter, storageKeyGenerator, thumbnailCreator,
            fileSystem, optionsMonitor, new NullLogger<AppetiserImageProcessor>());
    }
    
    [Fact]
    public async Task ProcessImage_CreatesAndRemovesRequiredDirectories()
    {
        // Arrange
        var context = IngestionContextFactory.GetIngestionContext(AssetIdGenerator.GetAssetId());

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
        var context = IngestionContextFactory.GetIngestionContext(AssetIdGenerator.GetAssetId(asset: "some(id)"));
        var expectedDirectory = $"scratch/{context.IngestId}/99/1/some_id_/output";
        
        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => fileSystem.CreateDirectory(
            A<string>.That.Matches(dir => dir.Replace("\\", "/") == expectedDirectory))).MustHaveHappenedOnceExactly();
        A.CallTo(() => fileSystem.DeleteDirectory(A<string>._, true, true)).MustHaveHappenedTwiceExactly();
    }
    
    [Fact]
    public async Task ProcessImage_False_IfImageProcessorCallFails()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);
        
        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId,
                A<IReadOnlyList<SizeParameter>>._,
                A<ImageProcessorOperations>._,
                A<CancellationToken>._))
            .Returns(new AppetiserResponseErrorModel
            {
                Message = "error message",
                Status = 422
            });


        // Act
        var result = await sut.ProcessImage(context);

        // Assert
        result.Should().BeFalse();
        context.Asset.Should().NotBeNull();
        context.Asset.Error.Should().Be("Appetiser Error: error message");
    }

    [Fact]
    public async Task ProcessImage_SetsOperation_ThumbsOnly_IfUseOriginal()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId, imageDeliveryChannelPolicy: "use-original");

        A.CallTo(() => appetiserClient.GenerateDerivatives(context,
                assetId,
                A<IReadOnlyList<SizeParameter>>._,
                A<ImageProcessorOperations>._,
                A<CancellationToken>._))
            .Returns(new AppetiserResponseModel { Height = 100, Width = 100 });

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                ImageProcessorOperations.Thumbnails, A<CancellationToken>._))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task ProcessImage_SetsOperation_ThumbsOnly_IfNoImageChannel()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId, imageDeliveryChannelPolicy: "");

        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                A<ImageProcessorOperations>._, A<CancellationToken>._))
            .Returns(new AppetiserResponseModel { Height = 100, Width = 100 });

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                ImageProcessorOperations.Thumbnails, A<CancellationToken>._))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task ProcessImage_SetsOperation_ImageAndThumbsOnly_IfDefaultImageChannel()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId, imageDeliveryChannelPolicy: "default");

        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                A<ImageProcessorOperations>._, A<CancellationToken>._))
            .Returns(new AppetiserResponseModel { Height = 100, Width = 100 });

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                ImageProcessorOperations.Thumbnails | ImageProcessorOperations.Derivative, A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task ProcessImage_UpdatesAssetDimensions()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
        };

        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                A<ImageProcessorOperations>._, A<CancellationToken>._))
            .Returns(imageProcessorResponse);

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
    public async Task ProcessImage_UploadsGeneratedFileToDlcsBucket_AndSetsImageLocation_IfNotUseOriginal(bool optimised,
        OriginStrategyType strategy)
    {
        // Arrange
        var assetId = new AssetId(1, 2, "test");
        var context = IngestionContextFactory.GetIngestionContext(assetId);

        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                A<ImageProcessorOperations>._, A<CancellationToken>._))
            .Returns(new AppetiserResponseModel
            {
                JP2 = $"scratch/{context.IngestId}/1/2/test/outputtest.jp2"
            });

        context.AssetFromOrigin!.CustomerOriginStrategy = new CustomerOriginStrategy
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
            .WithFilePath($"scratch/{context.IngestId}/1/2/test/outputtest.jp2")
            .WithContentType("image/jp2");
        context.ImageLocation!.S3.Should().Be(expected);
        context.StoredObjects.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessImage_UploadsFileToBucket_UsingLocationOnDisk_IfUseOriginal_AndNotOptimised()
    {
        // Arrange
        var assetId = new AssetId(1, 2, "test");
        const string locationOnDisk = "/file/on/disk";
        var context = IngestionContextFactory.GetIngestionContext(assetId, imageDeliveryChannelPolicy: "use-original");
        context.AssetFromOrigin!.Location = locationOnDisk;

        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                A<ImageProcessorOperations>._, A<CancellationToken>._))
            .Returns(new AppetiserResponseModel
            {
                JP2 = locationOnDisk,
            });

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
    public async Task ProcessImage_SetsImageLocation_WithoutUploading_IfUseOriginalAndOptimisedStrategy()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId, imageDeliveryChannelPolicy: "use-original",
                optimised: true);
        context.Asset.Origin = "https://s3.amazonaws.com/dlcs-storage/2/1/foo-bar";

        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                A<ImageProcessorOperations>._, A<CancellationToken>._))
            .Returns(new AppetiserResponseModel());

        const string expected = "s3://dlcs-storage/2/1/foo-bar";

        A.CallTo(() => storageKeyGenerator.GetS3Uri(A<ObjectInBucket>._, A<bool>._))
            .Returns(new Uri(expected));

        // Act
        await sut.ProcessImage(context);

        // Assert
        bucketWriter.Operations.Should().BeEmpty();
        context.ImageLocation!.S3.Should().Be(expected);
        context.StoredObjects.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessImage_ProcessesNewThumbs()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);
        
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Thumbs =
            [
                new ImageOnDisk { Height = 100, Width = 50, Path = "/path/to/thumb/100.jpg" }
            ],
        };

        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                A<ImageProcessorOperations>._, A<CancellationToken>._))
            .Returns(imageProcessorResponse);

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => thumbnailCreator.CreateNewThumbs(A<Asset>.That.Matches(a => a.Id == context.Asset.Id),
                A<IReadOnlyList<ImageOnDisk>>.That.Matches(t => t.Single().Path.EndsWith("100.jpg"))
            ))
            .MustHaveHappened();
    }

    [Fact]
    public async Task ProcessImage_SpecifiesUnionOfThumbSizes()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);
        
        IReadOnlyList<SizeParameter>? sizes = null;
        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                A<ImageProcessorOperations>._, A<CancellationToken>._))
            .Invokes((IngestionContext _, AssetId _, IReadOnlyList<SizeParameter> sp, ImageProcessorOperations _,
                CancellationToken _) => sizes = sp)
            .Returns(new AppetiserResponseModel());

        var expected = new List<SizeParameter>
        {
            SizeParameter.Parse("!1024,1024"),
            SizeParameter.Parse("!1000,1000"),
            SizeParameter.Parse("!400,400"),
            SizeParameter.Parse("!200,200"),
            SizeParameter.Parse("!100,100"),
        };

        // Act
        await sut.ProcessImage(context);

        // Assert
        sizes!.Should().BeEquivalentTo(expected, "Passed sizes is union of delivery policy and system defaults");
    }

    [Fact]
    public async Task ProcessImage_PassesSystemThumbs_WhenNoThumbsChannel()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId);
        context.Asset.ImageDeliveryChannels = new List<ImageDeliveryChannel>
        {
            new()
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault,
                DeliveryChannelPolicy = new DeliveryChannelPolicy { Name = "default" }
            }
        };

        IReadOnlyList<SizeParameter>? sizes = null;
        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                A<ImageProcessorOperations>._, A<CancellationToken>._))
            .Invokes((IngestionContext _, AssetId _, IReadOnlyList<SizeParameter> sp, ImageProcessorOperations _,
                CancellationToken _) => sizes = sp)
            .Returns(new AppetiserResponseModel());

        var expected = new List<SizeParameter>
        {
            SizeParameter.Parse("!1024,1024"),
            SizeParameter.Parse("!400,400"),
            SizeParameter.Parse("!200,200"),
            SizeParameter.Parse("!100,100"),
        };

        // Act
        await sut.ProcessImage(context);

        // Assert
        sizes!.Should().BeEquivalentTo(expected, "Passed sizes is union of delivery policy and system defaults");
    }

    [Fact]
    public async Task ProcessImage_UseOriginal_NoImageDeliveryChannel_CreatesThumbs_DoesNotStoreAsset()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
            Thumbs =
            [
                new ImageOnDisk { Path = "foo" }, new ImageOnDisk { Path = "bar" }
            ]
        };
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId, imageDeliveryChannelPolicy: "");

        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                A<ImageProcessorOperations>._, A<CancellationToken>._))
            .Returns(imageProcessorResponse);

        context.Asset.ImageDeliveryChannels = new List<ImageDeliveryChannel>
        {
            new()
            {
                Channel = AssetDeliveryChannels.Thumbnails,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault,
                DeliveryChannelPolicy = new DeliveryChannelPolicy
                {
                    PolicyData = "[\"1000,1000\",\"400,400\",\"200,200\",\"100,100\"]"
                }
            }
        };

        context.AssetFromOrigin!.Location = "/file/on/disk";
        context.Asset.Origin = "s3://origin/2/1/foo-bar";

        A.CallTo(() => fileSystem.GetFileSize(A<string>._)).Returns(100);

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() =>
                thumbnailCreator.CreateNewThumbs(context.Asset,
                    A<IReadOnlyList<ImageOnDisk>>.That.Matches(i => i.Count == 2)))
            .MustHaveHappened();
        context.ImageStorage!.ThumbnailSize.Should().Be(200, "Thumbs saved");
        context.ImageStorage.Size.Should().Be(0, "JP2 not written");
        bucketWriter.Operations.Should().BeEmpty();
        context.Asset.Height.Should().Be(1000);
        context.Asset.Width.Should().Be(5000);
        context.StoredObjects.Should().BeEmpty();
    }
    
    [Fact]
    public async Task ProcessImage_UseOriginal_AlreadyUploaded()
    {
        // Arrange
        var imageProcessorResponse = new AppetiserResponseModel
        {
            Height = 1000,
            Width = 5000,
            Thumbs =
            [
                new ImageOnDisk { Path = "foo" }, new ImageOnDisk { Path = "bar" }
            ]
        };
        
        var assetId = AssetIdGenerator.GetAssetId();
        var context = IngestionContextFactory.GetIngestionContext(assetId,
            cos: new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient },
            imageDeliveryChannelPolicy: "use-original");
        context.AssetFromOrigin!.Location = "/file/on/disk";
        context.Asset.Origin = "s3://origin/2/1/foo-bar";

        A.CallTo(() => appetiserClient.GenerateDerivatives(context, assetId, A<IReadOnlyList<SizeParameter>>._,
                A<ImageProcessorOperations>._, A<CancellationToken>._))
            .Returns(imageProcessorResponse);
        
        var alreadyUploadedFile = new RegionalisedObjectInBucket("appetiser-test", $"{context.Asset.Id}/original", "Fake-Region");
        context.StoredObjects.Add(alreadyUploadedFile, -999);

        A.CallTo(() => fileSystem.GetFileSize(A<string>._)).Returns(100);

        // Act
        await sut.ProcessImage(context);

        // Assert
        A.CallTo(() => thumbnailCreator.CreateNewThumbs(context.Asset, A<IReadOnlyList<ImageOnDisk>>._))
            .MustHaveHappened();
        context.ImageStorage!.ThumbnailSize.Should().Be(200, "Thumbs saved");
        context.ImageStorage.Size.Should().Be(0, "JP2 not written to S3");
        bucketWriter.Operations.Should().BeEmpty("JP2 not written to S3");
        context.Asset.Height.Should().Be(imageProcessorResponse.Height);
        context.Asset.Width.Should().Be(imageProcessorResponse.Width);
        context.StoredObjects.Should().ContainKey(alreadyUploadedFile).WhoseValue.Should()
            .Be(-999, "Value should not have changed");
    }
}
