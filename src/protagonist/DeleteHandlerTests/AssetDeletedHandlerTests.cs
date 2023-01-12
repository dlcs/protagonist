using System.Text.Json.Nodes;
using Amazon.S3;
using Amazon.S3.Model;
using DeleteHandler;
using DeleteHandler.Infrastructure;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using DLCS.AWS.SQS;
using DLCS.Core.FileSystem;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers;
using Test.Helpers.Integration;
using Test.Helpers.Storage;

namespace DeleteHandlerTests;

public class AssetDeletedHandlerTests
{
    private readonly DeleteHandlerSettings handlerSettings;
    private readonly IBucketWriter bucketWriter;
    private readonly FakeFileSystem fakeFileSystem;
    private readonly IStorageKeyGenerator storageKeyGenerator;

    public AssetDeletedHandlerTests()
    {
        handlerSettings = new DeleteHandlerSettings
        {
            AWS = new AWSSettings
            {
                S3 = new S3Settings
                {
                    StorageBucket = LocalStackFixture.StorageBucketName,
                    ThumbsBucket = LocalStackFixture.ThumbsBucketName
                }
            },
            ImageFolderTemplate = "/nas/{customer}/{space}/{image-dir}/{image}.jp2"
        };
        storageKeyGenerator = new S3StorageKeyGenerator(Options.Create(handlerSettings.AWS));
        bucketWriter = A.Fake<IBucketWriter>();
        fakeFileSystem = new FakeFileSystem();
    }

    public AssetDeletedHandler GetSut()
        => new(storageKeyGenerator, bucketWriter, fakeFileSystem, Options.Create(handlerSettings),
            new NullLogger<AssetDeletedHandler>());

    [Fact]
    public async Task Handle_ReturnsTrue_IfInvalidFormat()
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
        response.Should().BeTrue();
        fakeFileSystem.DeletedFiles.Should().BeEmpty();
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_ReturnsTrue_IfInvalidAssetId()
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
        response.Should().BeTrue();
        fakeFileSystem.DeletedFiles.Should().BeEmpty();
        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DeletesThumbs_Origin_AndNasFile()
    {
        // Arrange
        const string assetId = "1/99/foo";
        var queueMessage = new QueueMessage
        {
            Body = new JsonObject { ["id"] = assetId }
        };

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);
        
        // Assert
        response.Should().BeTrue();
        
        // File deleted from local disk
        fakeFileSystem.DeletedFiles.Should().ContainSingle(s => s == "/nas/1/99/foo/foo.jp2");
        
        // Thumbs deleted
        A.CallTo(() =>
            bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(a =>
                a[0].Bucket == LocalStackFixture.ThumbsBucketName && a[0].Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        A.CallTo(() =>
            bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(a =>
                a[0].Bucket == LocalStackFixture.StorageBucketName && a[0].Key == assetId
            ))).MustHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DoesNotDeleteFile_IfSettingEmpty()
    {
        // Arrange
        const string assetId = "1/99/foo";
        var queueMessage = new QueueMessage
        {
            Body = new JsonObject { ["id"] = assetId }
        };
        handlerSettings.ImageFolderTemplate = null;

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);
        
        // Assert
        response.Should().BeTrue();
        
        // File deleted from local disk
        fakeFileSystem.DeletedFiles.Should().BeEmpty();
        
        // Thumbs deleted
        A.CallTo(() =>
            bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(a =>
                a[0].Bucket == LocalStackFixture.ThumbsBucketName && a[0].Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        A.CallTo(() =>
            bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(a =>
                a[0].Bucket == LocalStackFixture.StorageBucketName && a[0].Key == assetId
            ))).MustHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DoesNotDeleteThumbs_IfSettingEmpty()
    {
        // Arrange
        const string assetId = "1/99/foo";
        var queueMessage = new QueueMessage
        {
            Body = new JsonObject { ["id"] = assetId }
        };
        handlerSettings.AWS.S3.ThumbsBucket = "";

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);
        
        // Assert
        response.Should().BeTrue();
        
        // File deleted from local disk
        fakeFileSystem.DeletedFiles.Should().ContainSingle(s => s == "/nas/1/99/foo/foo.jp2");
        
        // Thumbs deleted
        A.CallTo(() =>
            bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(a =>
                a[0].Bucket == LocalStackFixture.ThumbsBucketName && a[0].Key == $"{assetId}/"
            ))).MustNotHaveHappened();
        
        A.CallTo(() =>
            bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(a =>
                a[0].Bucket == LocalStackFixture.StorageBucketName && a[0].Key == assetId
            ))).MustHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DoesNotDeleteStorage_IfSettingEmpty()
    {
        // Arrange
        const string assetId = "1/99/foo";
        var queueMessage = new QueueMessage
        {
            Body = new JsonObject { ["id"] = assetId }
        };
        handlerSettings.AWS.S3.StorageBucket = "";

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);
        
        // Assert
        response.Should().BeTrue();
        
        // File deleted from local disk
        fakeFileSystem.DeletedFiles.Should().ContainSingle(s => s == "/nas/1/99/foo/foo.jp2");
        
        // Thumbs deleted
        A.CallTo(() =>
            bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(a =>
                a[0].Bucket == LocalStackFixture.ThumbsBucketName && a[0].Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        A.CallTo(() =>
            bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(a =>
                a[0].Bucket == LocalStackFixture.StorageBucketName && a[0].Key == assetId
            ))).MustNotHaveHappened();
    }
}