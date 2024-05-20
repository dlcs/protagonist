using System.Text.Json;
using System.Text.Json.Nodes;
using CleanupHandler;
using CleanupHandler.Infrastructure;
using CleanupHandler.Repository;
using DLCS.AWS.ElasticTranscoder;
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
    private readonly IElasticTranscoderWrapper elasticTranscoderWrapper;
    private readonly ICleanupHandlerAssetRepository cleanupHandlerAssetRepository;
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web);

    private readonly ImageDeliveryChannel imageDeliveryChannelUseOriginalImage = new()
    {
        Id = 999,
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
    
    private readonly ImageDeliveryChannel imageDeliveryChannelFile = new()
    {
        Id = KnownDeliveryChannelPolicies.FileNone,
        Channel = AssetDeliveryChannels.File,
        DeliveryChannelPolicy = new DeliveryChannelPolicy()
        {
            Id = KnownDeliveryChannelPolicies.ImageUseOriginal,
            Channel = AssetDeliveryChannels.File,
            Modified = DateTime.MinValue,
            Created = DateTime.MinValue
        },
        DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone
    };
    
    private readonly ImageDeliveryChannel imageDeliveryChannelThumbnail = new()
    {
        Channel = AssetDeliveryChannels.Thumbnails,
        Id = KnownDeliveryChannelPolicies.ThumbsDefault,
        DeliveryChannelPolicy = new DeliveryChannelPolicy()
        {
            Channel = AssetDeliveryChannels.Thumbnails,
            Created = DateTime.MinValue,
            Modified = DateTime.MinValue
        },
        DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault
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
        elasticTranscoderWrapper = A.Fake<IElasticTranscoderWrapper>();
        cleanupHandlerAssetRepository = A.Fake<ICleanupHandlerAssetRepository>();
        
        A.CallTo(() => thumbRepository.GetAllSizes(A<AssetId>._)).Returns(new List<int[]>()
        {
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
                400, 800
            },
            new[]
            {
                1024, 2048
            }
        });
    }

    private AssetUpdatedHandler GetSut()
        => new(storageKeyGenerator, bucketWriter, bucketReader, assetMetadataRepository, thumbRepository,
            Options.Create(handlerSettings), elasticTranscoderWrapper, engineClient, cleanupHandlerAssetRepository,
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
        var imageDeliveryChannelsBefore = new List<ImageDeliveryChannel>
        {
            imageDeliveryChannelFile
        };
        

        var requestDetails = CreateMinimalRequestDetails(imageDeliveryChannelsBefore, new List<ImageDeliveryChannel>(),
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
        var currentTime = DateTime.Now;
        
        var imageDeliveryChannelsBefore = new List<ImageDeliveryChannel>
        {
            new()
            {
                Channel = AssetDeliveryChannels.Timebased,
                Id = KnownDeliveryChannelPolicies.AvDefaultVideo,
                DeliveryChannelPolicy = new DeliveryChannelPolicy()
                {
                    Channel = AssetDeliveryChannels.Timebased,
                    Created = currentTime,
                    Modified = currentTime
                },
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.AvDefaultVideo
            }
        };
        

        var requestDetails = CreateMinimalRequestDetails(imageDeliveryChannelsBefore, new List<ImageDeliveryChannel>(),
            string.Empty, string.Empty);

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
    }
    
     [Fact]
    public async Task Handle_DeletesThumbnailAssets_WhenThumbnailChannelRemoved()
    {
        // Arrange
        var imageDeliveryChannelsBefore = new List<ImageDeliveryChannel>
        {
            imageDeliveryChannelThumbnail
        };

        var requestDetails = CreateMinimalRequestDetails(imageDeliveryChannelsBefore, new List<ImageDeliveryChannel>(),
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
                "1/99/foo/stuff/100.jpg", "1/99/foo/stuff/200.jpg", "1/99/foo/stuff/400.jpg", "1/99/foo/stuff/1024.jpg"
            });

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(requestDetails.queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o =>
                        o[0].Key == "1/99/foo/info/Cantaloupe/v3/info.json" &&
                        o[0].Bucket == handlerSettings.AWS.S3.StorageBucket)))
            .MustHaveHappened();
        A.CallTo(() => assetMetadataRepository.DeleteAssetApplicationMetadata(A<AssetId>._, A<string>._, A<CancellationToken>._)).MustHaveHappened();
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
        

        var requestDetails = CreateMinimalRequestDetails(imageDeliveryChannelsBefore, new List<ImageDeliveryChannel>() {imageDeliveryChannelUseOriginalImage},
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
                        o[0].Key == "1/99/foo/info/Cantaloupe/v3/info.json" &&
                        o[0].Bucket == handlerSettings.AWS.S3.StorageBucket)))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o =>
                        o[1].Key == "1/99/foo/stuff/2048.jpg" &&
                        o[1].Bucket == handlerSettings.AWS.S3.ThumbsBucket)))
            .MustHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DeletesValidPaths_WhenImageChannelRemoved()
    {
        // Arrange
        var imageDeliveryChannelsBefore = new List<ImageDeliveryChannel>
        {
            imageDeliveryChannelUseOriginalImage
        };
        

        var requestDetails = CreateMinimalRequestDetails(imageDeliveryChannelsBefore, new List<ImageDeliveryChannel>(),
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
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(o => o[1].Key == "1/99/foo/info/Cantaloupe/v3/info.json")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o[2].Key == "1/99/foo/original")))
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
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(o => o[1].Key == "1/99/foo/info/Cantaloupe/v3/info.json")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o.Any(o => o.Key == "1/99/foo/original"))))
            .MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DoesNotDeleteOriginal_WhenImageChannelRemovedWithThumbnailLeft()
    {
        // Arrange
        var imageDeliveryChannelsBefore = new List<ImageDeliveryChannel>
        {
            imageDeliveryChannelUseOriginalImage,
            imageDeliveryChannelThumbnail
        };
        

        var requestDetails = CreateMinimalRequestDetails(imageDeliveryChannelsBefore, new List<ImageDeliveryChannel>() { imageDeliveryChannelThumbnail },
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
                bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(o => o[1].Key == "1/99/foo/info/Cantaloupe/v3/info.json")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.DeleteFromBucket(
                    A<ObjectInBucket[]>.That.Matches(o => o.Any(o => o.Key == "1/99/foo/original"))))
            .MustNotHaveHappened();
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._)).MustNotHaveHappened();
    }

    // modified
    
    
    // updated
    
    
    // roles

    
    // helper functions
    
    private (QueueMessage queueMessage, Asset assetAfter) CreateMinimalRequestDetails(List<ImageDeliveryChannel> imageDeliveryChannelsBefore, 
        List<ImageDeliveryChannel> imageDeliveryChannelsAfter, string rolesBefore, string rolesAfter)
    {
        var assetBefore = new Asset()
        {
            Id = new AssetId(1, 99, "foo"),
            ImageDeliveryChannels = imageDeliveryChannelsBefore,
            Roles = rolesBefore
        };

        var assetAfter = new Asset()
        {
            Id = new AssetId(1, 99, "foo"),
            ImageDeliveryChannels = imageDeliveryChannelsAfter,
            Roles = rolesAfter
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
            Attributes = new Dictionary<string, string>()
            {
                { "engineNotified", "True" }
            }
        };
        return (queueMessage, assetAfter);
    }
}