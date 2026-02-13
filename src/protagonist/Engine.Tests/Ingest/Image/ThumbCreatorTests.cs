using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using Engine.Ingest.Image;
using Engine.Settings;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Storage;

namespace Engine.Tests.Ingest.Image;

public class ThumbCreatorTests
{
    private readonly TestBucketWriter bucketWriter;
    private readonly ThumbCreator sut;
    private readonly List<ImageDeliveryChannel> thumbsDeliveryChannel =
    [
        new()
        {
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault,
            Channel = AssetDeliveryChannels.Thumbnails
        }
    ];
    
    public ThumbCreatorTests()
    {
        bucketWriter = new TestBucketWriter();
        sut = GetSut();
    }

    private ThumbCreator GetSut(int maxWidth = 5000)
    {
        var storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        
        A.CallTo(() => storageKeyGenerator.GetThumbsSizesJsonLocation(A<AssetId>._))
            .ReturnsLazily((AssetId assetId) => new ObjectInBucket("thumbs-bucket", $"{assetId}/s.json"));
        A.CallTo(() => storageKeyGenerator.GetThumbnailLocation(A<AssetId>._, A<int>._, A<bool>._))
            .ReturnsLazily((AssetId assetId, int size, bool open) =>
            {
                var authSlug = open ? "o" : "a";
                return new ObjectInBucket("thumbs-bucket", $"{assetId}/{authSlug}/{size}.jpg");
            });
        var options = Options.Create(new EngineSettings { MaxWidth = maxWidth });
        return new ThumbCreator(bucketWriter, storageKeyGenerator, options, new NullLogger<ThumbCreator>());
    }

    [Fact]
    public async Task CreateNewThumbs_NoOp_IfThumbsToProcessEmpty()
    {
        // Arrange
        var asset = new Asset(new AssetId(10, 20, "foo"));
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, []);
        
        // Assert
        thumbsCreated.Should().Be(0);
    }
    
    [Theory]
    [InlineData(100)]
    [InlineData(0)]
    public async Task CreateNewThumbs_UploadsExpected_AllOpen(int openFullMax)
    {
        // All cases have MaxWidth=0 and no roles
        // OpenFullMax is ignored as no roles so doesn't matter if it has a value or not
        var assetId = new AssetId(10, 20, "foo");
        var asset = new Asset(assetId)
        {
            Width = 3030, Height = 5000, MaxWidth = 0, OpenFullMax = openFullMax,
            ImageDeliveryChannels = thumbsDeliveryChannel
        };

        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 606, Height = 1000, Path = "1000.jpg" },
            new() { Width = 302, Height = 500, Path = "500.jpg" },
            new() { Width = 60, Height = 100, Path = "100.jpg" }
        };
        
        const string thumbSizes = "{\"o\":[[606,1000],[302,500],[60,100]],\"a\":[]}";
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, imagesOnDisk);
        
        // Assert
        thumbsCreated.Should().Be(3);

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
            .WithContents(thumbSizes);
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
        asset.AssetApplicationMetadata.Should()
            .Contain(i => i.MetadataType == "ThumbSizes" && i.MetadataValue == thumbSizes);
    }
    
    [Fact]
    public async Task CreateNewThumbs_UploadsExpected_LargestFirst()
    {
        // Similar test to above but proves thumbs are ordered internally 
        var assetId = new AssetId(10, 20, "foo");
        var asset = new Asset(assetId)
        {
            Width = 3030, Height = 5000,
            ImageDeliveryChannels = thumbsDeliveryChannel
        };

        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 302, Height = 500, Path = "500.jpg" },
            new() { Width = 60, Height = 100, Path = "100.jpg" },
            new() { Width = 606, Height = 1000, Path = "1000.jpg" },
        };
        
        const string thumbSizes = "{\"o\":[[606,1000],[302,500],[60,100]],\"a\":[]}";
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, imagesOnDisk);
        
        // Assert
        thumbsCreated.Should().Be(3);

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
            .WithContents(thumbSizes);
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
        asset.AssetApplicationMetadata.Should()
            .Contain(i => i.MetadataType == "ThumbSizes" && i.MetadataValue == thumbSizes);
    }
    
    [Theory]
    [InlineData(1000)]
    [InlineData(500)]
    [InlineData(0)]
    public async Task CreateNewThumbs_UploadsExpected_LargestAuth_NoRoles(int openFullMax)
    {
        // MaxWidth is 700 and there are no roles so we won't create 'open' thumbs larger than 700
        // included OpenFullMax to show this has no effect as no roles 
        var assetId = new AssetId(10, 20, "foo");
        var asset = new Asset(assetId)
        {
            Width = 3030, Height = 5000, MaxWidth = 700, OpenFullMax = openFullMax,
            ImageDeliveryChannels = thumbsDeliveryChannel
        };

        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 606, Height = 1000, Path = "1000.jpg" },
            new() { Width = 302, Height = 500, Path = "500.jpg" },
            new() { Width = 60, Height = 100, Path = "100.jpg" }
        };
        
        const string thumbSizes = "{\"o\":[[302,500],[60,100]],\"a\":[[606,1000]]}";
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, imagesOnDisk);
        
        // Assert
        thumbsCreated.Should().Be(3);
        
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
            .WithContents(thumbSizes);
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
        asset.AssetApplicationMetadata.Should()
            .Contain(i => i.MetadataType == "ThumbSizes" && i.MetadataValue == thumbSizes);
    }
    
    [Fact]
    public async Task CreateNewThumbs_UploadsExpected_Restriction_FromSystemMaxWidth()
    {
        // Effective MaxWidth is 700 but this is set at the system, not asset, level
        var assetId = new AssetId(10, 20, "foo");
        var asset = new Asset(assetId)
        {
            Width = 3030, Height = 5000, ImageDeliveryChannels = thumbsDeliveryChannel
        };

        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 606, Height = 1000, Path = "1000.jpg" },
            new() { Width = 302, Height = 500, Path = "500.jpg" },
            new() { Width = 60, Height = 100, Path = "100.jpg" }
        };
        
        const string thumbSizes = "{\"o\":[[302,500],[60,100]],\"a\":[[606,1000]]}";
        
        // Act
        var thumbsCreated = await GetSut(700).CreateNewThumbs(asset, imagesOnDisk);
        
        // Assert
        thumbsCreated.Should().Be(3);
        
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
            .WithContents(thumbSizes);
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
        asset.AssetApplicationMetadata.Should()
            .Contain(i => i.MetadataType == "ThumbSizes" && i.MetadataValue == thumbSizes);
    }
    
    [Theory]
    [InlineData(700, 0)] // ofm only
    [InlineData(700, 700)] // both ofm and mw match
    [InlineData(700, 1024)] // mw is greater than ofm
    [InlineData(1024, 700)] // ofm is greater than mw
    public async Task CreateNewThumbs_UploadsExpected_LargestAuth_Roles(int openFullMax, int maxWidth)
    {
        // Asset has roles. In all cases the effective 'max' value is 700
        // This will be OpenFullMax or MaxWidth if one provided.
        // If both provided it is OpenFullMax unless that value is larger than MaxWidth 
        var assetId = new AssetId(10, 20, "foo");
        var asset = new Asset(assetId)
        {
            Width = 3030, Height = 5000, MaxWidth = maxWidth, OpenFullMax = openFullMax,
            ImageDeliveryChannels = thumbsDeliveryChannel, Roles = "https://test"
        };

        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 606, Height = 1000, Path = "1000.jpg" },
            new() { Width = 302, Height = 500, Path = "500.jpg" },
            new() { Width = 60, Height = 100, Path = "100.jpg" }
        };
        
        const string thumbSizes = "{\"o\":[[302,500],[60,100]],\"a\":[[606,1000]]}";
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, imagesOnDisk);
        
        // Assert
        thumbsCreated.Should().Be(3);
        
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
            .WithContents(thumbSizes);
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
        asset.AssetApplicationMetadata.Should()
            .Contain(i => i.MetadataType == "ThumbSizes" && i.MetadataValue == thumbSizes);
    }
    
    [Fact]
    public async Task CreateNewThumbs_UploadsExpected_AuthMatchesImageSize_OpenFullMax()
    {
        // MaxWidth is 500 and there are roles so we won't create 'open' thumbs larger than 500
        // 500 also matches a size, this test confirms we allow that size but no larger
        var assetId = new AssetId(10, 20, "foo");
        var asset = new Asset(assetId)
        {
            Width = 3030, Height = 5000, OpenFullMax = 500, Roles = "https://test",
            ImageDeliveryChannels = thumbsDeliveryChannel
        };

        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 606, Height = 1000, Path = "1000.jpg" },
            new() { Width = 302, Height = 500, Path = "500.jpg" },
            new() { Width = 60, Height = 100, Path = "100.jpg" }
        };
        
        const string thumbSizes = "{\"o\":[[302,500],[60,100]],\"a\":[[606,1000]]}";
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, imagesOnDisk);
        
        // Assert
        thumbsCreated.Should().Be(3);
        
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
            .WithContents(thumbSizes);
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
        asset.AssetApplicationMetadata.Should()
            .Contain(i => i.MetadataType == "ThumbSizes" && i.MetadataValue == thumbSizes);
    }
    
    [Fact]
    public async Task CreateNewThumbs_UploadsExpected_ImageSmallerThanThumbnail()
    {
        // Arrange
        var assetId = new AssetId(10, 20, "foo");
        var asset = new Asset(assetId)
        {
            Width = 266, Height = 440, ImageDeliveryChannels = thumbsDeliveryChannel
        };

        // NOTE - this handles multiple IIIF Image size parameters resulting in same image width
        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 266, Height = 440, Path = "1000.jpg" },
            new() { Width = 266, Height = 440, Path = "500.jpg" },
            new() { Width = 60, Height = 100, Path = "100.jpg" }
        };
        
        const string thumbSizes = "{\"o\":[[266,440],[60,100]],\"a\":[]}";
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, imagesOnDisk);
        
        // Assert
        thumbsCreated.Should().Be(2);
        
        bucketWriter
            .ShouldHaveKey("10/20/foo/o/440.jpg")
            .WithFilePath("1000.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/o/100.jpg")
            .WithFilePath("100.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/s.json")
            .WithContents(thumbSizes);
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
        asset.AssetApplicationMetadata.Should()
            .Contain(i => i.MetadataType == "ThumbSizes" && i.MetadataValue == thumbSizes);
    }
    
    [Fact]
    public async Task CreateNewThumbs_UploadsExpected_ThumbLargerThanImage()
    {
        // Arrange
        var assetId = new AssetId(10, 20, "foo");
        var asset = new Asset(assetId)
        {
            Width = 266, Height = 440, ImageDeliveryChannels = thumbsDeliveryChannel
        };

        // NOTE - this handles using upscaling thumbnail size
        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 532, Height = 880, Path = "880.jpg" },
            new() { Width = 266, Height = 440, Path = "500.jpg" },
            new() { Width = 60, Height = 100, Path = "100.jpg" }
        };
        
        const string thumbSizes = "{\"o\":[[532,880],[266,440],[60,100]],\"a\":[]}";
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, imagesOnDisk);
        
        // Assert
        thumbsCreated.Should().Be(3);
        
        bucketWriter
            .ShouldHaveKey("10/20/foo/o/880.jpg")
            .WithFilePath("880.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/o/440.jpg")
            .WithFilePath("500.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/o/100.jpg")
            .WithFilePath("100.jpg");
        bucketWriter
            .ShouldHaveKey("10/20/foo/s.json")
            .WithContents(thumbSizes);
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
        asset.AssetApplicationMetadata.Should()
            .Contain(i => i.MetadataType == "ThumbSizes" && i.MetadataValue == thumbSizes);
    }
    
    [Theory]
    [InlineData(256)]
    [InlineData(0)]
    public async Task CreateNewThumbs_UploadsExpected_AllAuth(int maxWidth)
    {
        // All cases have a role and no OpenFullMax (so none available as open)
        // Value of MaxWidth doesn't matter as none are available anonymously
        var assetId = new AssetId(10, 20, "foo");
        var asset = new Asset(assetId)
        {
            Width = 3030, Height = 5000, OpenFullMax = 0, Roles = "https://test",
            ImageDeliveryChannels = thumbsDeliveryChannel, MaxWidth = maxWidth
        };

        var imagesOnDisk = new List<ImageOnDisk>
        {
            new() { Width = 606, Height = 1000, Path = "1000.jpg" },
            new() { Width = 302, Height = 500, Path = "500.jpg" },
            new() { Width = 60, Height = 100, Path = "100.jpg" }
        };
        const string thumbSizes = "{\"o\":[],\"a\":[[606,1000],[302,500],[60,100]]}";
        
        // Act
        var thumbsCreated = await sut.CreateNewThumbs(asset, imagesOnDisk);
        
        // Assert
        thumbsCreated.Should().Be(3);
        
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
            .WithContents(thumbSizes);
        
        bucketWriter.ShouldHaveNoUnverifiedPaths();
        asset.AssetApplicationMetadata.Should()
            .Contain(i => i.MetadataType == "ThumbSizes" && i.MetadataValue == thumbSizes);
    }
}
