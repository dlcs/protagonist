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
    public async Task CreateNewThumbs_NoOp_IfExpectedThumbsEmpty()
    {
        // Arrange
        var asset = new Asset(new AssetId(10, 20, "foo"))
        {
            Width = 40, Height = 50,
            ImageDeliveryChannels = new List<ImageDeliveryChannel>
            {
                new()
                {
                    DeliveryChannelPolicyId = 1,
                    Channel = AssetDeliveryChannels.Thumbnails,
                    DeliveryChannelPolicy = new DeliveryChannelPolicy
                    {
                        PolicyData = "[]"
                    }
                }
            }
        };

        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, new[]
        {
            new ImageOnDisk
            {
                Height = 10, Path = "here", Width = 10
            }
        });
        
        // Assert
        thumbsCreated.Should().Be(0);
    }

    [Fact]
    public async Task CreateNewThumbs_UploadsExpected_AllOpen_NormalisedSizes()
    {
        // Arrange
        var asset = new Asset(new AssetId(10, 20, "foo"))
        {
            Width = 3030, Height = 5000,
            ImageDeliveryChannels = new List<ImageDeliveryChannel>
            {
                new()
                {
                    DeliveryChannelPolicyId = 1,
                    Channel = AssetDeliveryChannels.Thumbnails,
                    DeliveryChannelPolicy = new DeliveryChannelPolicy
                    {
                        PolicyData = "[\"1000,1000\",\"500,500\",\"100,100\"]"
                    }
                }
            }
        };

        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 606, Height = 1000, Path = "1000.jpg" },
            new() { Width = 302, Height = 500, Path = "500.jpg" }, // Should be 303, simulate rounding error
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
        
        // verify that s.json uses the calculated size, rather than size returned from processor
        bucketWriter
            .ShouldHaveKey("10/20/foo/s.json")
            .WithContents("{\"o\":[[606,1000],[303,500],[61,100]],\"a\":[]}");
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
    }
    
    [Fact]
    public async Task CreateNewThumbs_UploadsExpected_LargestAuth_NormalisedSizes()
    {
        // Arrange
        var asset = new Asset(new AssetId(10, 20, "foo"))
        {
            Width = 3030, Height = 5000, MaxUnauthorised = 700,
            ImageDeliveryChannels = new List<ImageDeliveryChannel>
            {
                new()
                {
                    DeliveryChannelPolicyId = 1,
                    Channel = AssetDeliveryChannels.Thumbnails,
                    DeliveryChannelPolicy = new DeliveryChannelPolicy
                    {
                        PolicyData = "[\"1000,1000\",\"500,500\",\"100,100\"]"
                    }
                }
            }
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
        
        // verify that s.json uses the calculated size, rather than size returned from processor
        bucketWriter
            .ShouldHaveKey("10/20/foo/s.json")
            .WithContents("{\"o\":[[303,500],[61,100]],\"a\":[[606,1000]]}");
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
    }
    
    [Fact]
    public async Task CreateNewThumbs_UploadsExpected_ImageSmallerThanThumbnail_NormalisedSizes()
    {
        // Arrange
        var asset = new Asset(new AssetId(10, 20, "foo"))
        {
            Width = 266, Height = 440,
            ImageDeliveryChannels = new List<ImageDeliveryChannel>
            {
                new()
                {
                    DeliveryChannelPolicyId = 1,
                    Channel = AssetDeliveryChannels.Thumbnails,
                    DeliveryChannelPolicy = new DeliveryChannelPolicy
                    {
                        PolicyData = "[\"1000,1000\",\"500,500\",\"100,100\"]"
                    }
                }
            }
        };

        // NOTE - this mimics the payload that Appetiser would send back
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
        
        // verify that s.json uses the calculated size, rather than size returned from processor
        bucketWriter
            .ShouldHaveKey("10/20/foo/s.json")
            .WithContents("{\"o\":[[266,440],[60,100]],\"a\":[]}");
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
    }
}
