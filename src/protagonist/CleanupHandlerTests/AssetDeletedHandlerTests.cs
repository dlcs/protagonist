using System.Text.Json.Nodes;
using Amazon.S3;
using Amazon.S3.Model;
using CleanupHandler;
using CleanupHandler.Infrastructure;
using DLCS.AWS.Cloudfront;
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
    private readonly CleanupHandlerSettings handlerSettings;
    private readonly IBucketWriter bucketWriter;
    private readonly FakeFileSystem fakeFileSystem;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly ICacheInvalidator cacheInvalidator;

    public AssetDeletedHandlerTests()
    {
        handlerSettings = new CleanupHandlerSettings
        {
            AWS = new AWSSettings
            {
                S3 = new S3Settings
                {
                    StorageBucket = LocalStackFixture.StorageBucketName,
                    ThumbsBucket = LocalStackFixture.ThumbsBucketName,
                    OutputBucket = LocalStackFixture.OutputBucketName,
                    OriginBucket = LocalStackFixture.OriginBucketName
                }
            },
            ImageFolderTemplate = "/nas/{customer}/{space}/{image-dir}/{image}.jp2"
        };
        storageKeyGenerator = new S3StorageKeyGenerator(Options.Create(handlerSettings.AWS));
        bucketWriter = A.Fake<IBucketWriter>();
        fakeFileSystem = new FakeFileSystem();
        cacheInvalidator = A.Fake<ICacheInvalidator>();
    }

    private AssetDeletedHandler GetSut()
        => new(storageKeyGenerator, bucketWriter, cacheInvalidator ,fakeFileSystem, Options.Create(handlerSettings),
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
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.ThumbsBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        // storage deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.StorageBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        // origin deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.OriginBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        // output deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.OutputBucketName && a.Key == $"{assetId}/"
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
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.ThumbsBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        // storage deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.StorageBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        // origin deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.OriginBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        // output deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.OutputBucketName && a.Key == $"{assetId}/"
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
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.ThumbsBucketName && a.Key == $"{assetId}/"
            ))).MustNotHaveHappened();
        
        // storage deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.StorageBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        // origin deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.OriginBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        // output deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.OutputBucketName && a.Key == $"{assetId}/"
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
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.ThumbsBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        
        // storage not deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.StorageBucketName && a.Key == $"{assetId}/"
            ))).MustNotHaveHappened();
        
        // origin deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.OriginBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        // output deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.OutputBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DoesNotDeleteOrigin_IfSettingEmpty()
    {
        // Arrange
        const string assetId = "1/99/foo";
        var queueMessage = new QueueMessage
        {
            Body = new JsonObject { ["id"] = assetId }
        };
        handlerSettings.AWS.S3.OriginBucket = "";

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);
        
        // Assert
        response.Should().BeTrue();
        
        // File deleted from local disk
        fakeFileSystem.DeletedFiles.Should().ContainSingle(s => s == "/nas/1/99/foo/foo.jp2");
        
        // Thumbs deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.ThumbsBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        
        // storage not deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.StorageBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        // origin deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.OriginBucketName && a.Key == $"{assetId}/"
            ))).MustNotHaveHappened();
        
        // output deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.OutputBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
    }
    
    [Fact]
    public async Task Handle_DoesNotDeleteOutput_IfSettingEmpty()
    {
        // Arrange
        const string assetId = "1/99/foo";
        var queueMessage = new QueueMessage
        {
            Body = new JsonObject { ["id"] = assetId }
        };
        handlerSettings.AWS.S3.OriginBucket = "";

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);
        
        // Assert
        response.Should().BeTrue();
        
        // File deleted from local disk
        fakeFileSystem.DeletedFiles.Should().ContainSingle(s => s == "/nas/1/99/foo/foo.jp2");
        
        // Thumbs deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.ThumbsBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        
        // storage not deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.StorageBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
        
        // origin deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.OriginBucketName && a.Key == $"{assetId}/"
            ))).MustNotHaveHappened();
        
        // output deleted
        A.CallTo(() =>
            bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(a =>
                a.Bucket == LocalStackFixture.OutputBucketName && a.Key == $"{assetId}/"
            ))).MustHaveHappened();
    }
    
    [Fact]
    public async Task Handle_InvalidatesImagePath_IfDeliveryChannels()
    {
        // Arrange
        const string assetId = "1/99/foo";
        var queueMessage = new QueueMessage
        {
            Body = new JsonObject { ["id"] = assetId , ["deliveryChannels"] = new JsonArray()
            {
                "iiif-img",
                "file",
                "iiif-av"
            }}
        };
        handlerSettings.AWS.Cloudfront.DistributionId = "someValue";

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);
        
        // Assert
        response.Should().BeTrue();
        
        // File deleted from local disk
        fakeFileSystem.DeletedFiles.Should().ContainSingle(s => s == "/nas/1/99/foo/foo.jp2");
        
        
        // invalidates images
        A.CallTo(() =>
            cacheInvalidator.InvalidateCdnCache(A<List<string>>.That.Contains("/iiif-img/1/99/foo/*"), 
                A<CancellationToken>._)).MustHaveHappened();
        
        // invalidates files
        A.CallTo(() =>
            cacheInvalidator.InvalidateCdnCache(A<List<string>>.That.Contains("/file/1/99/foo/*"),
                A<CancellationToken>._)).MustHaveHappened();
        
        // invalidates av
        A.CallTo(() =>
            cacheInvalidator.InvalidateCdnCache(A<List<string>>.That.Contains("/iiif-av/1/99/foo/*"),
                A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task Handle_DoesNotInvalidateImagePath_IfNoDeliveryChannels()
    {
        // Arrange
        const string assetId = "1/99/foo";
        var queueMessage = new QueueMessage
        {
            Body = new JsonObject { ["id"] = assetId }

        };
        handlerSettings.AWS.Cloudfront.DistributionId = "someValue";

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);

        // Assert
        response.Should().BeTrue();

        // File deleted from local disk
        fakeFileSystem.DeletedFiles.Should().ContainSingle(s => s == "/nas/1/99/foo/foo.jp2");


        // invalidates images
        A.CallTo(() =>
            cacheInvalidator.InvalidateCdnCache(A<List<string>>._,
                A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_InvalidatesImagePath_IfImageAssetFamily()
    {
        // Arrange
        const string assetId = "1/99/foo";
        const string assetFamily = "I";
        var queueMessage = new QueueMessage
        {
            Body = new JsonObject { ["id"] = assetId , ["assetFamily"] = assetFamily }

        };
        handlerSettings.AWS.Cloudfront.DistributionId = "someValue";

        // Act
        var sut = GetSut();
        var response = await sut.HandleMessage(queueMessage);

        // Assert
        response.Should().BeTrue();

        // File deleted from local disk
        fakeFileSystem.DeletedFiles.Should().ContainSingle(s => s == "/nas/1/99/foo/foo.jp2");


        // invalidates images
        A.CallTo(() =>
            cacheInvalidator.InvalidateCdnCache(A<List<string>>._,
                A<CancellationToken>._)).MustHaveHappened();
    }
}