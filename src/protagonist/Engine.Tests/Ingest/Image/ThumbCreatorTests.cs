using System.Collections.ObjectModel;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using Engine.Ingest.Image;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Storage;

namespace Engine.Tests.Ingest.Image;

public class ThumbCreatorTests
{
    private readonly TestBucketWriter bucketWriter;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly ThumbCreator sut;
    private readonly List<ImageDeliveryChannel> thumbsDeliveryChannel = new()
    {
        new ImageDeliveryChannel()
        {
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault,
            Channel = AssetDeliveryChannels.Thumbnails
        }
    };
    
    public ThumbCreatorTests()
    {
        bucketWriter = new TestBucketWriter();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();

        A.CallTo(() => storageKeyGenerator.GetLargestThumbnailLocation(A<AssetId>._))
            .ReturnsLazily((AssetId assetId) => new ObjectInBucket("thumbs-bucket", $"{assetId}/low.jpg"));
        A.CallTo(() => storageKeyGenerator.GetThumbsSizesJsonLocation(A<AssetId>._))
            .ReturnsLazily((AssetId assetId) => new ObjectInBucket("thumbs-bucket", $"{assetId}/s.json"));
        A.CallTo(() => storageKeyGenerator.GetThumbnailLocation(A<AssetId>._, A<int>._, A<bool>._))
            .ReturnsLazily((AssetId assetId, int size, bool open) =>
            {
                var authSlug = open ? "o" : "a";
                return new ObjectInBucket("thumbs-bucket", $"{assetId}/{authSlug}/{size}.jpg");
            });

        sut = new ThumbCreator(bucketWriter, storageKeyGenerator, new NullLogger<ThumbCreator>());
    }

    [Fact]
    public async Task CreateNewThumbs_NoOp_IfThumbsToProcessEmpty()
    {
        // Arrange
        var asset = new Asset(new AssetId(10, 20, "foo"));
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, Array.Empty<ImageOnDisk>());
        
        // Assert
        thumbsCreated.Should().Be(0);
    }
    
    [Fact]
    public async Task CreateNewThumbs_UploadsExpected_AllOpen()
    {
        // Arrange
        var asset = new Asset(new AssetId(10, 20, "foo"))
        {
            Width = 3030, Height = 5000, MaxUnauthorised = -1,
            ImageDeliveryChannels = thumbsDeliveryChannel
        };

        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 606, Height = 1000, Path = "1000.jpg" },
            new() { Width = 302, Height = 500, Path = "500.jpg" },
            new() { Width = 60, Height = 100, Path = "100.jpg" }
        };
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, imagesOnDisk);
        
        // Assert
        thumbsCreated.Should().Be(3);

        bucketWriter
            .ShouldHaveKey("10/20/foo/low.jpg")
            .WithFilePath("1000.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/o/1000.jpg")
            .WithFilePath("1000.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/o/500.jpg")
            .WithFilePath("500.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/o/100.jpg")
            .WithFilePath("100.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/s.json")
            .WithContents("{\"o\":[[606,1000],[302,500],[60,100]],\"a\":[]}");
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
    }
    
    [Fact]
    public async Task CreateNewThumbs_UploadsExpected_LargestAuth()
    {
        // Arrange
        var asset = new Asset(new AssetId(10, 20, "foo"))
        {
            Width = 3030, Height = 5000, MaxUnauthorised = 700,
            ImageDeliveryChannels = thumbsDeliveryChannel
        };

        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 606, Height = 1000, Path = "1000.jpg" },
            new() { Width = 302, Height = 500, Path = "500.jpg" },
            new() { Width = 60, Height = 100, Path = "100.jpg" }
        };
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, imagesOnDisk);
        
        // Assert
        thumbsCreated.Should().Be(3);

        bucketWriter
            .ShouldHaveKey("10/20/foo/low.jpg")
            .WithFilePath("1000.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/a/1000.jpg")
            .WithFilePath("1000.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/o/500.jpg")
            .WithFilePath("500.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/o/100.jpg")
            .WithFilePath("100.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/s.json")
            .WithContents("{\"o\":[[302,500],[60,100]],\"a\":[[606,1000]]}");
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
    }
    
    [Fact]
    public async Task CreateNewThumbs_UploadsExpected_ImageSmallerThanThumbnail()
    {
        // Arrange
        var asset = new Asset(new AssetId(10, 20, "foo"))
        {
            Width = 266, Height = 440,
            ImageDeliveryChannels = thumbsDeliveryChannel
        };

        // NOTE - this handles multiple IIIF Image size parameters resulting in same image width
        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 266, Height = 440, Path = "1000.jpg" },
            new() { Width = 266, Height = 440, Path = "500.jpg" },
            new() { Width = 60, Height = 100, Path = "100.jpg" }
        };
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, imagesOnDisk);
        
        // Assert
        thumbsCreated.Should().Be(2);

        bucketWriter
            .ShouldHaveKey("10/20/foo/low.jpg")
            .WithFilePath("1000.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/o/440.jpg")
            .WithFilePath("1000.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/o/100.jpg")
            .WithFilePath("100.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/s.json")
            .WithContents("{\"o\":[[266,440],[60,100]],\"a\":[]}");
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
    }
    
    [Fact]
    public async Task CreateNewThumbs_UploadsNothing_MaxUnauthorisedIs0()
    {
        // Arrange
        var asset = new Asset(new AssetId(10, 20, "foo"))
        {
            Width = 3030, Height = 5000, MaxUnauthorised = 0,
            ImageDeliveryChannels = thumbsDeliveryChannel
        };

        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 606, Height = 1000, Path = "1000.jpg" },
            new() { Width = 302, Height = 500, Path = "500.jpg" },
            new() { Width = 60, Height = 100, Path = "100.jpg" }
        };
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, imagesOnDisk);
        
        // Assert
        thumbsCreated.Should().Be(3);

        bucketWriter
            .ShouldHaveKey("10/20/foo/low.jpg")
            .WithFilePath("1000.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/a/1000.jpg")
            .WithFilePath("1000.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/a/500.jpg")
            .WithFilePath("500.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/a/100.jpg")
            .WithFilePath("100.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/s.json")
            .WithContents("{\"o\":[],\"a\":[[606,1000],[302,500],[60,100]]}");
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
    }
}
