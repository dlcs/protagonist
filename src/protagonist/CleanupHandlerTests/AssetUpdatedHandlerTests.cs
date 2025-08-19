using System.Text.Json;
using System.Text.Json.Nodes;
using CleanupHandler;
using CleanupHandler.Infrastructure;
using CleanupHandler.Repository;
using DLCS.AWS.ElasticTranscoder.Models;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using DLCS.AWS.SQS;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using DLCS.Model.Messaging;
using DLCS.Model.PathElements;
using DLCS.Model.Policies;
using DLCS.Repository.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;

namespace DeleteHandlerTests;

public class AssetUpdatedHandlerTests
{
    private readonly CleanupHandlerSettings handlerSettings;
    private readonly IBucketWriter bucketWriter;
    private readonly IBucketReader bucketReader;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IAssetApplicationMetadataRepository assetMetadataRepository;
    private readonly IEngineClient engineClient;
    private readonly IThumbRepository thumbRepository;
    private readonly ICleanupHandlerAssetRepository cleanupHandlerAssetRepository;
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web);

    private readonly ImageDeliveryChannel imageDeliveryChannelUseOriginalImage = new()
    {
        Id = 567587,
        Channel = AssetDeliveryChannels.Image,
        DeliveryChannelPolicy = new DeliveryChannelPolicy()
        {
            Id = KnownDeliveryChannelPolicies.ImageUseOriginal,
            Channel = AssetDeliveryChannels.Image,
            Modified = DateTime.MinValue,
            Created = DateTime.MinValue
        },
        DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageUseOriginal
    };

    private readonly ImageDeliveryChannel imageDeliveryChannelDefaultImage = new()
    {
        Id = 58465678,
        Channel = AssetDeliveryChannels.Image,
        DeliveryChannelPolicy = new DeliveryChannelPolicy()
        {
            Id = KnownDeliveryChannelPolicies.ImageDefault,
            Channel = AssetDeliveryChannels.Image,
            Modified = DateTime.MinValue,
            Created = DateTime.MinValue
        },
        DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
    };
    
    private readonly ImageDeliveryChannel imageDeliveryChannelFile = new()
    {
        Id = 56785678,
        Channel = AssetDeliveryChannels.File,
        DeliveryChannelPolicy = new DeliveryChannelPolicy()
        {
            Id = KnownDeliveryChannelPolicies.FileNone,
            Channel = AssetDeliveryChannels.File,
            Modified = DateTime.MinValue,
            Created = DateTime.MinValue
        },
        DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone
    };
    
    private readonly ImageDeliveryChannel imageDeliveryChannelThumbnail = new()
    {
        Channel = AssetDeliveryChannels.Thumbnails,
        Id = 34256,
        DeliveryChannelPolicy = new DeliveryChannelPolicy()
        {
            Channel = AssetDeliveryChannels.Thumbnails,
            Id = KnownDeliveryChannelPolicies.ThumbsDefault,
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue
        },
        DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault
    };

    private readonly ImageDeliveryChannel imageDeliveryChannelTimebased = new()
    {
        Channel = AssetDeliveryChannels.Timebased,
        Id = 356367,
        DeliveryChannelPolicy = new DeliveryChannelPolicy()
        {
            Channel = AssetDeliveryChannels.Timebased,
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue
        },
        DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.AvDefaultVideo
    };

    public AssetUpdatedHandlerTests()
    {
        handlerSettings = new CleanupHandlerSettings
        {
            AWS = new AWSSettings
            {
                S3 = new S3Settings
                {
                    StorageBucket = LocalStackFixture.StorageBucketName,
                    ThumbsBucket = LocalStackFixture.ThumbsBucketName,
                    OriginBucket = LocalStackFixture.OriginBucketName
                }
            },
            ImageFolderTemplate = "/nas/{customer}/{space}/{image-dir}/{image}.jp2",
            AssetModifiedSettings = new AssetModifiedSettings()
            {
                DryRun = false
            }
        };
        storageKeyGenerator = new S3StorageKeyGenerator(Options.Create(handlerSettings.AWS));
        bucketWriter = A.Fake<IBucketWriter>();
        bucketReader = A.Fake<IBucketReader>();
        engineClient = A.Fake<IEngineClient>();
        assetMetadataRepository = A.Fake<IAssetApplicationMetadataRepository>();
        thumbRepository = A.Fake<IThumbRepository>();
        cleanupHandlerAssetRepository = A.Fake<ICleanupHandlerAssetRepository>();
        
        A.CallTo(() => thumbRepository.GetAllSizes(A<AssetId>._)).Returns(new List<int[]>()
        {
            new[]
            {
                50, 100
            },
            new[]
            {
                100, 200
            },
            new[]
            {
                200, 400
            },
            new[]
            {
                516, 1024
            }
        });
    }

    private AssetUpdatedHandler GetSut()
        => new(storageKeyGenerator, bucketWriter, bucketReader, assetMetadataRepository, thumbRepository,
            Options.Create(handlerSettings), engineClient, cleanupHandlerAssetRepository,
            new NullLogger<AssetUpdatedHandler>());
    
    [Fact]
    public async Task Handle_ReturnsFalse_IfInvalidFormat()
    {
        // Arrange
        var queueMessage = new QueueMessage
        {
            Body = new JsonObject { ["not-id"] = "foo" }
        };

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);
        
        // Assert
        response.Should().BeFalse();
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    // removed
    
    [Fact]
    public async Task Handle_DeletesOriginal_WhenFileChannelRemoved()
    {
        // Arrange
        var requestDetails = CreateMinimalRequestDetails(new List<ImageDeliveryChannel>() { imageDeliveryChannelFile },
            new List<ImageDeliveryChannel>(),
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(o => o[0].Key == "1/99/foo/original")))
            .MustHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DoesNotDeleteOriginal_WhenFileChannelRemovedWithUseOriginalPolicy()
    {
        // Arrange
        var imageDeliveryChannelsBefore = new List<ImageDeliveryChannel>
        {
            imageDeliveryChannelFile,
            imageDeliveryChannelUseOriginalImage
        };

        var requestDetails = CreateMinimalRequestDetails(imageDeliveryChannelsBefore,
            new List<ImageDeliveryChannel>() { imageDeliveryChannelUseOriginalImage },
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DeletesTimebasedAssets_WhenTimebasedChannelRemoved()
    {
        // Arrange
        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel>() { imageDeliveryChannelTimebased }, new List<ImageDeliveryChannel>(),
            string.Empty, string.Empty, "video/mp3");

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
        
        A.CallTo(() => bucketReader.GetMatchingKeys(A<ObjectInBucket>._))
            .Returns(new []{ "1/99/foo/full/full/max/max/0/default.mp4", "1/99/foo/some/other/key" });

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(o => o[0].Key == "1/99/foo/metadata")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o[1].Key == "1/99/foo/full/full/max/max/0/default.mp4")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o.Any(x => x.Key  == "1/99/foo/some/other/key"))))
            .MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
        
        A.CallTo(() =>
                assetMetadataRepository.DeleteAssetApplicationMetadata(A<AssetId>._, "AVTranscodes",
                    A<CancellationToken>._))
            .Returns(true);
    }
    
    [Fact]
    public async Task Handle_DeletesThumbnailAssets_WhenThumbnailChannelRemoved()
    {
        // Arrange
        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel>() { imageDeliveryChannelThumbnail }, new List<ImageDeliveryChannel>(),
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
        A.CallTo(() =>
                assetMetadataRepository.DeleteAssetApplicationMetadata(A<AssetId>._, "ThumbSizes",
                    A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => bucketReader.GetMatchingKeys(A<ObjectInBucket>._))
            .Returns(new[]
            {
                "1/99/foo/stuff/100.jpg", "1/99/foo/stuff/200.jpg", "1/99/foo/stuff/400.jpg", "1/99/foo/stuff/1024.jpg"
            });

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() => assetMetadataRepository.DeleteAssetApplicationMetadata(A<AssetId>._, "ThumbSizes", A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() =>
            bucketWriter.DeleteFolder(
                A<ObjectInBucket>.That.Matches(o =>
                    o.Key == "1/99/foo/" && o.Bucket == handlerSettings.AWS.S3.ThumbsBucket),
                A<bool>.That.Matches(r => r == true))).MustHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DeletesSomeThumbnailAssets_WhenThumbnailChannelRemovedWithImageChannel()
    {
        // Arrange
        var imageDeliveryChannelsBefore = new List<ImageDeliveryChannel>
        {
            imageDeliveryChannelThumbnail,
            imageDeliveryChannelUseOriginalImage
        };

        var requestDetails = CreateMinimalRequestDetails(imageDeliveryChannelsBefore,
            new List<ImageDeliveryChannel>() { imageDeliveryChannelUseOriginalImage },
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
        A.CallTo(() =>
                assetMetadataRepository.DeleteAssetApplicationMetadata(A<AssetId>._, A<string>._,
                    A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => bucketReader.GetMatchingKeys(A<ObjectInBucket>._))
            .Returns(new[]
            {
                "1/99/foo/stuff/100.jpg", "1/99/foo/stuff/200.jpg", "1/99/foo/stuff/400.jpg", "1/99/foo/stuff/1024.jpg",
                "1/99/foo/stuff/2048.jpg"
            });

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o =>
                        o[0].Key == "1/99/foo/stuff/2048.jpg" &&
                        o[0].Bucket == handlerSettings.AWS.S3.ThumbsBucket)))
            .MustHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DeletesSomePortraitThumbnailAssets_WhenThumbnailChannelRemovedWithImageChannel()
    {
        // Arrange
        var imageDeliveryChannelsBefore = new List<ImageDeliveryChannel>
        {
            imageDeliveryChannelThumbnail,
            imageDeliveryChannelUseOriginalImage
        };

        var requestDetails = CreateMinimalRequestDetails(imageDeliveryChannelsBefore,
            new List<ImageDeliveryChannel>() { imageDeliveryChannelUseOriginalImage },
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
        A.CallTo(() =>
                assetMetadataRepository.DeleteAssetApplicationMetadata(A<AssetId>._, A<string>._,
                    A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => bucketReader.GetMatchingKeys(A<ObjectInBucket>._))
            .Returns(new[]
            {
                "1/99/foo/stuff/100.jpg", "1/99/foo/stuff/200.jpg", "1/99/foo/stuff/400.jpg", "1/99/foo/stuff/1024.jpg",
                "1/99/foo/stuff/2048.jpg"
            });
        
        A.CallTo(() => thumbRepository.GetAllSizes(A<AssetId>._)).Returns(new List<int[]>()
        {
            new[]
            {
                100, 50

            },
            new[]
            {
                200, 100

            },
            new[]
            {
                400, 200
            },
            new[]
            {
                1024, 516
            }
        });

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o =>
                        o[0].Key == "1/99/foo/stuff/2048.jpg" &&
                        o[0].Bucket == handlerSettings.AWS.S3.ThumbsBucket)))
            .MustHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DeletesValidPaths_WhenImageChannelRemoved()
    {
        // Arrange
        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel>() { imageDeliveryChannelUseOriginalImage }, new List<ImageDeliveryChannel>(),
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(o => o[0].Key == "1/99/foo")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o[1].Key == "1/99/foo/original")))
            .MustHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DoesNotDeleteOriginal_WhenImageChannelRemovedWithFileLeft()
    {
        // Arrange
        var imageDeliveryChannelsBefore = new List<ImageDeliveryChannel>
        {
            imageDeliveryChannelUseOriginalImage,
            imageDeliveryChannelFile
        };
        
        var requestDetails = CreateMinimalRequestDetails(imageDeliveryChannelsBefore, new List<ImageDeliveryChannel>() { imageDeliveryChannelFile },
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(o => o[0].Key == "1/99/foo")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o.Any(o => o.Key == "1/99/foo/original"))))
            .MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    // modified
    
    [Fact]
    public async Task Handle_DoesNotRemoveAnything_WhenFileChannelModified()
    {
        // Arrange
        var fileDeliveryChannelAfter = new ImageDeliveryChannel()
        {
            Id = 34512245,
            Channel = AssetDeliveryChannels.File,
            DeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Id = 34534,
                Channel = AssetDeliveryChannels.File,
                Modified = DateTime.MinValue,
                Created = DateTime.MinValue,
                Name = "some-delivery-channel"
            },
            DeliveryChannelPolicyId = 34534
        };

        var requestDetails = CreateMinimalRequestDetails(new List<ImageDeliveryChannel> { imageDeliveryChannelFile },
            new List<ImageDeliveryChannel>() { fileDeliveryChannelAfter },
            string.Empty, string.Empty);
    
        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
    
        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DeletesUnneededTimebasedAssets_WhenTimebasedChannelModified()
    {
        // Arrange
        var imageDeliveryChannelAfter = new ImageDeliveryChannel()
        {
            Channel = AssetDeliveryChannels.Timebased,
            Id = 152445,
            DeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Id = 8239,
                Channel = AssetDeliveryChannels.Timebased,
                Created = DateTime.MinValue,
                Modified = DateTime.MinValue,
                PolicyData = "[\"webm-policy\", \"oga-policy\"]"
            },
            DeliveryChannelPolicyId = 8239
        };

        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel>() { imageDeliveryChannelTimebased },
            new List<ImageDeliveryChannel>() { imageDeliveryChannelAfter },
            string.Empty, string.Empty, "video/*");

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
        
        A.CallTo(() => engineClient.GetAvPresets(A<CancellationToken>._)).Returns(new Dictionary<string, TranscoderPreset>()
        {
            { "webm-policy", new ("", "some-webm-preset", "oga") },
            { "oga-policy", new ("", "some-oga-preset", "webm") }
        });
        
        A.CallTo(() => bucketReader.GetMatchingKeys(A<ObjectInBucket>._))
            .Returns(new []{ "1/99/foo/full/full/max/max/0/default.mp4", "1/99/foo/some/other/key", "1/99/foo/full/full/max/max/0/default.webm", "1/99/foo/full/full/max/max/0/default.oga" });

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(o => o[0].Key == "1/99/foo/full/full/max/max/0/default.mp4")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o.Any(x => x.Key  == "1/99/foo/full/full/max/max/0/default.webm"))))
            .MustNotHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o.Any(x => x.Key  == "1/99/foo/some/other/key"))))
            .MustNotHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o.Any(x => x.Key  == "1/99/foo/full/full/max/max/0/default.oga"))))
            .MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    } 
    
    [Fact]
    public async Task Handle_DoesNothing_WhenTimebasedChannelUpdatedWithInvalidPreset()
    {
        // Arrange
        var imageDeliveryChannelAfter = new ImageDeliveryChannel()
        {
            Channel = AssetDeliveryChannels.Timebased,
            Id = 23456,
            DeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Id = 8239,
                Channel = AssetDeliveryChannels.Timebased,
                Created = DateTime.MinValue,
                Modified = DateTime.MinValue,
                PolicyData = "[\"policy-not-found\"]"
            },
            DeliveryChannelPolicyId = 8239
        };
        
        A.CallTo(() => engineClient.GetAvPresets(A<CancellationToken>._)).Returns(new Dictionary<string, TranscoderPreset>()
        {
            { "webm-policy", new ("", "some-webm-preset", "oga") },
            { "oga-policy", new ("", "some-oga-preset", "webm") }
        });

        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel>() { imageDeliveryChannelTimebased },
            new List<ImageDeliveryChannel>() { imageDeliveryChannelAfter },
            string.Empty, string.Empty, "video/*");

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
        
        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeFalse("Unable to fetch Preset details so fail handling");
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_ReturnsFalse_WhenTimebasedChannelUpdatedWithNoAvPresets()
    {
        // Arrange
        var imageDeliveryChannelAfter = new ImageDeliveryChannel()
        {
            Channel = AssetDeliveryChannels.Timebased,
            Id = 23456,
            DeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Id = 8239,
                Channel = AssetDeliveryChannels.Timebased,
                Created = DateTime.MinValue,
                Modified = DateTime.MinValue,
                PolicyData = "[\"policy-not-found\"]"
            },
            DeliveryChannelPolicyId = 8239
        };
        
        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel>() { imageDeliveryChannelTimebased },
            new List<ImageDeliveryChannel>() { imageDeliveryChannelAfter },
            string.Empty, string.Empty, "video/*");

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
        
        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeFalse();
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DoesNothing_WhenTimebasedChannelModfiedWithInvalidPresetDetails()
    {
        // Arrange
        var imageDeliveryChannelAfter = new ImageDeliveryChannel()
        {
            Channel = AssetDeliveryChannels.Timebased,
            Id = 345634,
            DeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Channel = AssetDeliveryChannels.Timebased,
                Id = 8239,
                Created = DateTime.MinValue,
                Modified = DateTime.MinValue,
                PolicyData = "[\"some-policy\"]"
            },
            DeliveryChannelPolicyId = 8239
        };

        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel>() { imageDeliveryChannelTimebased },
            new List<ImageDeliveryChannel>() { imageDeliveryChannelAfter },
            string.Empty, string.Empty, "video/*");

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
        
        A.CallTo(() => engineClient.GetAvPresets(A<CancellationToken>._)).Returns(new Dictionary<string, TranscoderPreset>()
        {
            { "some-policy", new ("", "some-transcode-preset", "") }
        });

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
        [Fact]
    public async Task Handle_DeletesSomeThumbnailAssets_WhenThumbnailChannelModifed()
    {
        // Arrange
        var imageDeliveryChannelsAfter = new List<ImageDeliveryChannel>
        {
            new ()
            {
                Channel = AssetDeliveryChannels.Thumbnails,
                Id = 42356,
                DeliveryChannelPolicy = new DeliveryChannelPolicy()
                {
                    Id = 35467,
                    Channel = AssetDeliveryChannels.Thumbnails,
                    Created = DateTime.MinValue,
                    Modified = DateTime.MinValue
                },
                DeliveryChannelPolicyId = 35467
            }
        };

        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel> { imageDeliveryChannelThumbnail }, imageDeliveryChannelsAfter,
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
        A.CallTo(() =>
                assetMetadataRepository.DeleteAssetApplicationMetadata(A<AssetId>._, A<string>._,
                    A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => bucketReader.GetMatchingKeys(A<ObjectInBucket>._))
            .Returns(new[]
            {
                "1/99/foo/stuff/100.jpg", "1/99/foo/stuff/200.jpg", "1/99/foo/stuff/400.jpg", "1/99/foo/stuff/1024.jpg",
                "1/99/foo/stuff/2048.jpg" , "1/99/full/100,200/0/default.jpg"
            });

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o =>
                        o[0].Key == "1/99/foo/stuff/2048.jpg" &&
                        o[0].Bucket == handlerSettings.AWS.S3.ThumbsBucket)))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o =>
                        o[1].Key == "1/99/full/100,200/0/default.jpg" &&
                        o[1].Bucket == handlerSettings.AWS.S3.ThumbsBucket)))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o.Any(x => x.Key == "1/99/foo/stuff/200.jpg"))))
            .MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DeletesValidPaths_WhenImageChannelUpdatedToDefault()
    {
        // Arrange
        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel> { imageDeliveryChannelUseOriginalImage }, new List<ImageDeliveryChannel> { imageDeliveryChannelDefaultImage },
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(o => o[0].Key == "1/99/foo/original")))
            .MustHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DeletesValidPaths_WhenImageChannelUpdatedToUseOriginal()
    {
        // Arrange
        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel> { imageDeliveryChannelDefaultImage }, new List<ImageDeliveryChannel> { imageDeliveryChannelUseOriginalImage },
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(o => o[0].Key == "1/99/foo")))
            .MustHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DoesNotDeleteAnything_WhenImageChannelUpdatedToUseDefaultWithFileChannel()
    {
        // Arrange
        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel> { imageDeliveryChannelUseOriginalImage },
            new List<ImageDeliveryChannel> { imageDeliveryChannelDefaultImage, imageDeliveryChannelFile },
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    // updated
    
    [Fact]
    public async Task Handle_DoesNotRemoveAnything_WhenFilePolicyUpdated()
    {
        // Arrange
        var fileDeliveryChannelAfter = new ImageDeliveryChannel()
        {
            Id = 56785678,
            Channel = AssetDeliveryChannels.File,
            DeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Id = KnownDeliveryChannelPolicies.FileNone,
                Channel = AssetDeliveryChannels.File,
                Modified = DateTime.MaxValue,
                Created = DateTime.MaxValue,
                Name = "some-delivery-channel"
            },
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone
        };

        var requestDetails = CreateMinimalRequestDetails(new List<ImageDeliveryChannel> { imageDeliveryChannelFile },
            new List<ImageDeliveryChannel>() { fileDeliveryChannelAfter },
            string.Empty, string.Empty);
    
        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
    
        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DeletesUnneededTimebasedAssets_WhenTimebasedChannelUpdated()
    {
        // Arrange
        var imageDeliveryChannelAfter = new ImageDeliveryChannel()
        {
            Channel = AssetDeliveryChannels.Timebased,
            Id = 356367,
            DeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Id = KnownDeliveryChannelPolicies.AvDefaultVideo,
                Channel = AssetDeliveryChannels.Timebased,
                Created = DateTime.MinValue,
                Modified = DateTime.MaxValue,
                PolicyData = "[\"webm-policy\", \"oga-policy\"]"
            },
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.AvDefaultVideo
        };

        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel>() { imageDeliveryChannelTimebased },
            new List<ImageDeliveryChannel>() { imageDeliveryChannelAfter },
            string.Empty, string.Empty, "video/*");

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
        
        A.CallTo(() => engineClient.GetAvPresets(A<CancellationToken>._)).Returns(new Dictionary<string, TranscoderPreset>()
        {
            { "webm-policy", new ("", "some-webm-preset", "oga") },
            { "oga-policy", new ("", "some-oga-preset", "webm") }
        });

        A.CallTo(() => bucketReader.GetMatchingKeys(A<ObjectInBucket>._))
            .Returns(new []{ "1/99/foo/full/full/max/max/0/default.mp4", "1/99/foo/some/other/key", "1/99/foo/full/full/max/max/0/default.webm", "1/99/foo/full/full/max/max/0/default.oga" });

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(o => o[0].Key == "1/99/foo/full/full/max/max/0/default.mp4")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o.Any(x => x.Key  == "1/99/foo/full/full/max/max/0/default.webm"))))
            .MustNotHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o.Any(x => x.Key  == "1/99/foo/some/other/key"))))
            .MustNotHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o.Any(x => x.Key  == "1/99/foo/full/full/max/max/0/default.oga"))))
            .MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    } 
    
    [Fact]
    public async Task Handle_DoesNothing_WhenTimebasedPolicyUpdatedWithInvalidPreset()
    {
        // Arrange
        var imageDeliveryChannelAfter = new ImageDeliveryChannel()
        {
            Channel = AssetDeliveryChannels.Timebased,
            Id = 356367,
            DeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Id = KnownDeliveryChannelPolicies.AvDefaultVideo,
                Channel = AssetDeliveryChannels.Timebased,
                Created = DateTime.MinValue,
                Modified = DateTime.MaxValue,
                PolicyData = "[\"policy-not-found\"]"
            },
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.AvDefaultVideo
        };

        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel>() { imageDeliveryChannelTimebased },
            new List<ImageDeliveryChannel>() { imageDeliveryChannelAfter },
            string.Empty, string.Empty, "video/*");

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
        
        A.CallTo(() => engineClient.GetAvPresets(A<CancellationToken>._)).Returns(new Dictionary<string, TranscoderPreset>()
        {
            { "some-policy", new ("some-transcode-preset", "", "") }
        });

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeFalse("Unable to fetch Preset details so fail handling");
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DoesNothing_WhenTimebasedPolicyUpdatedWithInvalidPresetDetails()
    {
        // Arrange
        var imageDeliveryChannelAfter = new ImageDeliveryChannel()
        {
            Channel = AssetDeliveryChannels.Timebased,
            Id = 356367,
            DeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Channel = AssetDeliveryChannels.Timebased,
                Id = KnownDeliveryChannelPolicies.AvDefaultVideo,
                Created = DateTime.MinValue,
                Modified = DateTime.MaxValue,
                PolicyData = "[\"some-policy\"]"
            },
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.AvDefaultVideo
        };

        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel>() { imageDeliveryChannelTimebased },
            new List<ImageDeliveryChannel>() { imageDeliveryChannelAfter },
            string.Empty, string.Empty, "video/*");

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
        
        A.CallTo(() => engineClient.GetAvPresets(A<CancellationToken>._)).Returns(new Dictionary<string, TranscoderPreset>()
        {
            { "some-policy", new ("", "some-transcode-preset", "") }
        });

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DeletesSomeThumbnailAssets_WhenThumbnailPolicyUpdated()
    {
        // Arrange
        var imageDeliveryChannelsAfter = new List<ImageDeliveryChannel>
        {
            new ()
            {
                Channel = AssetDeliveryChannels.Thumbnails,
                Id = 356367,
                DeliveryChannelPolicy = new DeliveryChannelPolicy
                {
                    Id = KnownDeliveryChannelPolicies.AvDefaultVideo,
                    Channel = AssetDeliveryChannels.Thumbnails,
                    Created = DateTime.MinValue,
                    Modified = DateTime.MaxValue
                },
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.AvDefaultVideo
            }
        };

        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel> { imageDeliveryChannelThumbnail }, imageDeliveryChannelsAfter,
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);
        A.CallTo(() =>
                assetMetadataRepository.DeleteAssetApplicationMetadata(A<AssetId>._, A<string>._,
                    A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => bucketReader.GetMatchingKeys(A<ObjectInBucket>._))
            .Returns(new[]
            {
                "1/99/foo/stuff/100.jpg", "1/99/foo/stuff/200.jpg", "1/99/foo/stuff/400.jpg", "1/99/foo/stuff/1024.jpg",
                "1/99/foo/stuff/2048.jpg", "1/99/full/100,200/0/default.jpg"
            });

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o =>
                        o[0].Key == "1/99/foo/stuff/2048.jpg" &&
                        o[0].Bucket == handlerSettings.AWS.S3.ThumbsBucket)))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o =>
                        o[1].Key == "1/99/full/100,200/0/default.jpg" &&
                        o[1].Bucket == handlerSettings.AWS.S3.ThumbsBucket)))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o.Any(x => x.Key == "1/99/foo/stuff/200.jpg"))))
            .MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DeletesValidPaths_WhenImageChannelIsUpdatedDefaultImagePolicy()
    {
        // Arrange
        var imageDeliveryChannelDefaultUpdated = new ImageDeliveryChannel()
        {
            Id = 58465678,
            Channel = AssetDeliveryChannels.Image,
            DeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Id = KnownDeliveryChannelPolicies.ImageDefault,
                Channel = AssetDeliveryChannels.Image,
                Created = DateTime.MinValue,
                Modified = DateTime.MaxValue
            },
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
        };
        
        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel> { imageDeliveryChannelDefaultImage }, new List<ImageDeliveryChannel> { imageDeliveryChannelDefaultUpdated },
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(o => o[0].Key == "1/99/foo/original")))
            .MustHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DeletesValidPaths_WhenImageChannelHasUpdatedUseOrginalPolicy()
    {
        // Arrange
        var imageDeliveryChannelUseOriginalUpdated = new ImageDeliveryChannel()
        {
            Id = 567587,
            Channel = AssetDeliveryChannels.Image,
            DeliveryChannelPolicy = new DeliveryChannelPolicy()
            {
                Id = KnownDeliveryChannelPolicies.ImageUseOriginal,
                Channel = AssetDeliveryChannels.Image,
                Created = DateTime.MinValue,
                Modified = DateTime.MaxValue
            },
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageUseOriginal
        };
        
        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel> { imageDeliveryChannelUseOriginalImage }, new List<ImageDeliveryChannel> { imageDeliveryChannelUseOriginalUpdated },
            string.Empty, string.Empty);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(o => o[0].Key == "1/99/foo")))
            .MustHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    // roles
    
    [Theory]
    [InlineData("", "new role")]
    [InlineData(null, "new role")]
    [InlineData("old role", null)]
    public async Task Handle_DeletesInfoJson_WhenRolesChanged(string? rolesBefore, string? rolesAfter)
    {
        // Arrange
        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel>() { imageDeliveryChannelUseOriginalImage }, new List<ImageDeliveryChannel>() { imageDeliveryChannelUseOriginalImage },
            string.Empty, "new role");

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(o => o.Key == "1/99/foo/info/"), A<bool>._)).MustHaveHappened();
    }
    
    [Theory]
    [InlineData("", null)]
    [InlineData(null, "")]
    [InlineData(null, null)]
    public async Task Handle_DeletesInfoJson_WhenRolesChangedBothNullOrEmpty(string? rolesBefore, string? rolesAfter)
    {
        // Arrange
        var requestDetails = CreateMinimalRequestDetails(
            new List<ImageDeliveryChannel>() { imageDeliveryChannelUseOriginalImage }, new List<ImageDeliveryChannel>() { imageDeliveryChannelUseOriginalImage },
            rolesBefore, rolesAfter);

        A.CallTo(() => cleanupHandlerAssetRepository.RetrieveAssetWithDeliveryChannels(A<AssetId>._))
            .Returns(requestDetails.assetAfter);

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
            bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(o => o.Key == "1/99/foo/info/"), A<bool>._)).MustNotHaveHappened();
    }

    // helper functions
    
    private (QueueMessage queueMessage, Asset assetAfter) CreateMinimalRequestDetails(List<ImageDeliveryChannel> imageDeliveryChannelsBefore, 
        List<ImageDeliveryChannel> imageDeliveryChannelsAfter, string? rolesBefore, string? rolesAfter, string mediaType = "image/jpg")
    {
        var assetBefore = new Asset()
        {
            Id = new AssetId(1, 99, "foo"),
            ImageDeliveryChannels = imageDeliveryChannelsBefore,
            Roles = rolesBefore,
            MediaType = mediaType
        };

        var assetAfter = new Asset()
        {
            Id = new AssetId(1, 99, "foo"),
            ImageDeliveryChannels = imageDeliveryChannelsAfter,
            Roles = rolesAfter,
            MediaType = mediaType
        };
        
        var cleanupRequest = new AssetUpdatedNotificationRequest()
        {
            AssetBeforeUpdate = assetBefore,
            CustomerPathElement = new CustomerPathElement(99, "stuff"),
            AssetAfterUpdate = assetAfter
        };

        var serialized = JsonSerializer.Serialize(cleanupRequest, settings);

        var queueMessage = new QueueMessage
        {
            Body = JsonNode.Parse(serialized)!.AsObject(),
            MessageAttributes = new Dictionary<string, string>()
            {
                { "engineNotified", "True" }
            }
        };
        return (queueMessage, assetAfter);
    }
}
