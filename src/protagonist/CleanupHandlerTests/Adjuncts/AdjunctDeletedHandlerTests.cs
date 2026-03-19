using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CleanupHandler.Adjunct;
using CleanupHandler.Infrastructure;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using DLCS.AWS.SQS;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Repository;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;

namespace DeleteHandlerTests.Adjuncts;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class AdjunctDeletedHandlerTests 
{
    private readonly CleanupHandlerSettings handlerSettings;
    private readonly DlcsContext dbContext;
    private readonly IAdjunctBucketOperations adjunctBucketOperations;
    private readonly IBucketWriter bucketWriter;
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public AdjunctDeletedHandlerTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
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
        
        bucketWriter = A.Fake<IBucketWriter>();
        var storageKeyGenerator = new S3StorageKeyGenerator(Options.Create(handlerSettings.AWS));
        
        adjunctBucketOperations = new AdjunctBucketOperations(new NullLogger<AdjunctBucketOperations>(), storageKeyGenerator, bucketWriter);
    }
    
    private AdjunctDeletedHandler GetSut()
        => new(adjunctBucketOperations, dbContext, Options.Create(handlerSettings),
            new NullLogger<AdjunctDeletedHandler>());
    
    [Fact]
    public async Task Handle_ReturnsFalse_IfNoAssetId()
    {
        // Arrange
        var queueMessage = new QueueMessage
        {
            Body = new JsonObject { ["id"] = "foo" }
        };

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);
        
        // Assert
        response.Should().BeFalse();
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_ReturnsTrue_IfValidAdjunct()
    {
        // Arrange
        var cleanupRequest = new DeletedNotificationRequest<Adjunct>
        {
            Deliverable = new Adjunct
            {
                Id = "someId",
                MediaType = "a-mediaType",
                IIIFLink = IIIFLinkType.Annotations,
                Type = "a-type",
                AssetId = new AssetId(1, 1, "someAsset"),
                Origin = "https://some.origin"
            }
        };
        
        var serialized = JsonSerializer.Serialize(cleanupRequest, settings);
        
        var queueMessage = new QueueMessage
        {
            Body = JsonNode.Parse(serialized)!.AsObject()
        };

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task Handle_ReturnsTrueWithoutDeletion_WhenNotHosted()
    {
        // Arrange
        var cleanupRequest = new DeletedNotificationRequest<Adjunct>
        {
            Deliverable = new Adjunct
            {
                Id = "someId",
                MediaType = "a-mediaType",
                IIIFLink = IIIFLinkType.Annotations,
                Type = "a-type",
                AssetId = new AssetId(1, 1, "someAsset")
            }
        };
        
        var serialized = JsonSerializer.Serialize(cleanupRequest, settings);
        
        var queueMessage = new QueueMessage
        {
            Body = JsonNode.Parse(serialized)!.AsObject()
        };

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_ReturnsTrueWithoutDeletion_IfAdjunctInDatabase()
    {
        // Arrange
        var assetId = new AssetId(1, 1, "someAsset");
        var asset = await dbContext.Images.AddTestAsset(assetId).WithTestAdjunct("someAdjunct");
        await dbContext.SaveChangesAsync();

        var adjunct = asset.Entity.Adjuncts!.First();
        
        var cleanupRequest = new DeletedNotificationRequest<Adjunct>
        {
            Deliverable = adjunct
        };
        
        var serialized = JsonSerializer.Serialize(cleanupRequest, settings);
        
        var queueMessage = new QueueMessage
        {
            Body = JsonNode.Parse(serialized)!.AsObject()
        };

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);
        
        // Assert
        response.Should().BeTrue();
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
    }
}
