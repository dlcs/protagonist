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
            ImageFolderTemplate = "/nas/{customer}/{space}/{image-dir}/{image}.jp2"
        };
        storageKeyGenerator = new S3StorageKeyGenerator(Options.Create(handlerSettings.AWS));
        bucketWriter = A.Fake<IBucketWriter>();
        bucketReader = A.Fake<IBucketReader>();
        engineClient = A.Fake<IEngineClient>();
        assetMetadataRepository = A.Fake<IAssetApplicationMetadataRepository>();
        thumbRepository = A.Fake<IThumbRepository>();
        elasticTranscoderWrapper = A.Fake<IElasticTranscoderWrapper>();
        cleanupHandlerAssetRepository = A.Fake<ICleanupHandlerAssetRepository>();
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
    
    private QueueMessage CreateMinimalQueueMessage(List<ImageDeliveryChannel> imageDeliveryChannelsBefore)
    {
        var cleanupRequest = new AssetUpdatedNotificationRequest()
        {
            AssetBeforeUpdate = new Asset()
            {
                Id = new AssetId(1, 99, "foo"),
                ImageDeliveryChannels = imageDeliveryChannelsBefore
            },
            CustomerPathElement = new CustomerPathElement(99, "stuff"),
            AssetAfterUpdate = new Asset()
            {
                Id = new AssetId(1, 99, "foo")
            }
        };

        var serialized = JsonSerializer.Serialize(cleanupRequest, settings);

        var queueMessage = new QueueMessage
        {
            Body = JsonNode.Parse(serialized)!.AsObject()
        };
        return queueMessage;
    }
}