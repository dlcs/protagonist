using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using DLCS.Core.Types;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Tests.S3;

public class S3StorageKeyGeneratorTests
{
    private readonly S3StorageKeyGenerator sut;

    public S3StorageKeyGeneratorTests()
    {
        sut = new S3StorageKeyGenerator(Options.Create(new AWSSettings
        {
            Region = "eu-west-1",
            S3 = new S3Settings
            {
                OutputBucket = "test-output",
                ThumbsBucket = "test-thumbs",
                StorageBucket = "test-storage",
                OriginBucket = "test-origin",
                SecurityObjectsBucket = "test-security",
                TimebasedInputBucket = "timebased-in",
                TimebasedOutputBucket = "timebased-out"
            }
        }));
    }
    
    [Fact]
    public void GetStorageKey_AssetId_ReturnsExpected()
    {
        // Arrange
        const string expected = "10/20/foo-bar";
        var asset = new AssetId(10, 20, "foo-bar");

        // Act
        var actual = S3StorageKeyGenerator.GetStorageKey(asset);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void GetStorageLocation_ReturnsExpected()
    {
        // Arrange
        const string expected = "10/20/foo-bar";
        var asset = new AssetId(10, 20, "foo-bar");

        // Act
        var actual = sut.GetStorageLocation(asset);
        
        // Assert
        actual.Key.Should().Be(expected);
        actual.Bucket.Should().Be("test-storage");
        actual.Region.Should().Be("eu-west-1");
    }
    
    [Fact]
    public void GetStoredOriginalLocation_ReturnsExpected()
    {
        // Arrange
        const string expected = "10/20/foo-bar/original";
        var asset = new AssetId(10, 20, "foo-bar");

        // Act
        var actual = sut.GetStoredOriginalLocation(asset);
        
        // Assert
        actual.Key.Should().Be(expected);
        actual.Bucket.Should().Be("test-storage");
        actual.Region.Should().Be("eu-west-1");
    }

    [Fact]
    public void GetThumbnailLocation_ReturnsExpected_Open()
    {
        // Arrange
        const string expected = "10/20/foo-bar/open/400.jpg";
        var asset = new AssetId(10, 20, "foo-bar");
        
        // Act
        var actual = sut.GetThumbnailLocation(asset, 400);
        
        // Assert
        actual.Key.Should().Be(expected);
        actual.Bucket.Should().Be("test-thumbs");
    }
    
    [Fact]
    public void GetThumbnailLocation_ReturnsExpected_Auth()
    {
        // Arrange
        const string expected = "10/20/foo-bar/auth/200.jpg";
        var asset = new AssetId(10, 20, "foo-bar");
        
        // Act
        var actual = sut.GetThumbnailLocation(asset, 200, false);
        
        // Assert
        actual.Key.Should().Be(expected);
        actual.Bucket.Should().Be("test-thumbs");
    }
    
    [Fact]
    public void GetLegacyThumbnailLocation_ReturnsExpected()
    {
        // Arrange
        const string expected = "10/20/foo-bar/full/200,800/0/default.jpg";
        var asset = new AssetId(10, 20, "foo-bar");
        
        // Act
        var actual = sut.GetLegacyThumbnailLocation(asset, 200, 800);
        
        // Assert
        actual.Key.Should().Be(expected);
        actual.Bucket.Should().Be("test-thumbs");
    }
    
    [Fact]
    public void GetThumbsSizesJsonLocation_ReturnsExpected()
    {
        // Arrange
        const string expected = "10/20/foo-bar/s.json";
        var asset = new AssetId(10, 20, "foo-bar");
        
        // Act
        var actual = sut.GetThumbsSizesJsonLocation(asset);
        
        // Assert
        actual.Key.Should().Be(expected);
        actual.Bucket.Should().Be("test-thumbs");
    }
    
    [Fact]
    public void GetThumbnailsRoot_ReturnsExpected()
    {
        // Arrange
        const string expected = "10/20/foo-bar/";
        var asset = new AssetId(10, 20, "foo-bar");
        
        // Act
        var actual = sut.GetThumbnailsRoot(asset);
        
        // Assert
        actual.Key.Should().Be(expected);
        actual.Bucket.Should().Be("test-thumbs");
    }
    
    [Fact]
    public void GetOutputLocation_ReturnsExpected()
    {
        // Act
        var actual = sut.GetOutputLocation("foo/bar/baz");
        
        // Assert
        actual.Key.Should().Be("foo/bar/baz");
        actual.Bucket.Should().Be("test-output");
    }

    [Fact]
    public void GetTimebasedAssetLocation_ReturnsExpected()
    {
        // Arrange
        const string expected = "10/20/foo-bar/foo/bar/baz";
        var asset = new AssetId(10, 20, "foo-bar");
        
        // Act
        var actual = sut.GetTimebasedAssetLocation(asset, "foo/bar/baz");
        
        // Assert
        actual.Key.Should().Be(expected);
        actual.Bucket.Should().Be("test-storage");
        actual.Region.Should().Be("eu-west-1");
    }
    
    [Fact]
    public void GetTimebasedAssetLocation_WithKey_Correct()
    {
        // Arrange
        const string key = "1/2/hello/file.mp4";
        
        // Act
        var result = sut.GetTimebasedAssetLocation(key);
        
        // Assert
        result.Bucket.Should().Be("test-storage");
        result.Key.Should().Be(key);
    }
    
    [Fact]
    public void GetInfoJsonRoot_WithKey_Correct()
    {
        // Arrange
        var asset = new AssetId(10, 20, "foo-bar");
        
        // Act
        var result = sut.GetInfoJsonRoot(asset);
        
        // Assert
        result.Bucket.Should().Be("test-storage");
        result.Key.Should().Be("10/20/foo-bar/info/");
    }

    [Theory]
    [InlineData("Cantaloupe", IIIF.ImageApi.Version.V2, "10/20/foo-bar/info/Cantaloupe/v2/info.json")]
    [InlineData("Cantaloupe", IIIF.ImageApi.Version.V3, "10/20/foo-bar/info/Cantaloupe/v3/info.json")]
    [InlineData("IIPImage", IIIF.ImageApi.Version.V2, "10/20/foo-bar/info/IIPImage/v2/info.json")]
    public void GetInfoJsonLocation_ReturnsExpected(string imageServer, IIIF.ImageApi.Version version,
        string expected)
    {
        // Arrange
        var asset = new AssetId(10, 20, "foo-bar");

        // Act
        var actual = sut.GetInfoJsonLocation(asset, imageServer, version);

        // Assert
        actual.Key.Should().Be(expected);
        actual.Bucket.Should().Be("test-storage");
    }

    [Fact]
    public void EnsureRegionSet_DoesNotChange_IfAlreadySet()
    {
        // Arrange
        const string region = "us-east-2";
        var regionalisedObject = new RegionalisedObjectInBucket("bucket", "foo", region);
        
        // Act
        sut.EnsureRegionSet(regionalisedObject);
        
        // Assert
        regionalisedObject.Region.Should().Be(region);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EnsureRegionSet_SetsToDefault_IfNullOrWhitespace(string region)
    {
        // Arrange
        var regionalisedObject = new RegionalisedObjectInBucket("bucket", "foo", region);
        
        // Act
        sut.EnsureRegionSet(regionalisedObject);
        
        // Assert
        regionalisedObject.Region.Should().Be("eu-west-1");
    }

    [Fact]
    public void GetTimebasedInputLocation_ReturnsExpected_WithRandomPostfix()
    {
        // Arrange
        const string expectedStart = "10/20/foo-bar/";
        var asset = new AssetId(10, 20, "foo-bar");
        
        string GetRandomPostfix(string key) => key.Split('/')[^1];
        
        // Act
        var actual = sut.GetTimebasedInputLocation(asset);
        var actual2 = sut.GetTimebasedInputLocation(asset);

        var randomBit = GetRandomPostfix(actual.Key);
        var randomBit2 = GetRandomPostfix(actual2.Key);
        
        // Assert
        actual.Key.Should().StartWith(expectedStart);
        actual.Bucket.Should().Be("timebased-in");
        
        actual2.Key.Should().StartWith(expectedStart);
        actual2.Bucket.Should().Be("timebased-in");

        randomBit.Should().NotBe(randomBit2);
    }

    [Fact]
    public void GetTimebasedInputLocation_WithKey_Correct()
    {
        // Arrange
        const string key = "1/2/hello/file.mp4";
        
        // Act
        var result = sut.GetTimebasedInputLocation(key);
        
        // Assert
        result.Bucket.Should().Be("timebased-in");
        result.Key.Should().Be(key);
    }
    
    [Fact]
    public void GetTimebasedOutputLocation_WithKey_Correct()
    {
        // Arrange
        const string key = "1/2/hello/file.mp4";
        
        // Act
        var result = sut.GetTimebasedOutputLocation(key);
        
        // Assert
        result.Bucket.Should().Be("timebased-out");
        result.Key.Should().Be(key);
    }

    [Fact]
    public void GetTimebasedMetadataLocation_Correct()
    {
        // Arrange
        var asset = new AssetId(10, 20, "foo-bar");

        // Act
        var actual = sut.GetTimebasedMetadataLocation(asset);

        // Assert
        actual.Key.Should().Be("10/20/foo-bar/metadata");
        actual.Bucket.Should().Be("test-storage");
    }
    
    [Fact]
    public void GetOriginStrategyCredentialsLocation_WithKey_Correct()
    {
        // Arrange
        const int customerId = 10;
        var strategyId = Guid.NewGuid().ToString();
        
        // Act
        var result = sut.GetOriginStrategyCredentialsLocation(customerId, strategyId);
        
        // Assert
        result.Bucket.Should().Be("test-security");
        result.Key.Should().Be($"{customerId}/origin-strategy/{strategyId}/credentials.json");
    }
    
    [Fact]
    public void GetOriginRoot_Correct()
    {
        // Arrange
        var assetId = new AssetId(10, 20, "foo-bar");
        
        // Act
        var result = sut.GetOriginRoot(assetId);
        
        // Assert
        result.Bucket.Should().Be("test-origin");
        result.Key.Should().Be("10/20/foo-bar/");
    }
    
    [Fact]
    public void GetTransientImageLocation_Correct()
    {
        // Arrange
        var assetId = new AssetId(10, 20, "foo-bar");
        
        // Act
        var result = sut.GetTransientImageLocation(assetId);
        
        // Assert
        result.Bucket.Should().Be("test-storage");
        result.Key.Should().Be("transient/10/20/foo-bar");
    }
    
    [Fact]
    public void GetTranscodeDestinationRoot_Correct()
    {
        // Arrange
        var assetId = new AssetId(10, 20, "foo-bar");
        var jobId = Guid.NewGuid().ToString();
        
        // Act
        var result = sut.GetTranscodeDestinationRoot(assetId, jobId);
        
        // Assert
        result.Bucket.Should().Be("timebased-out");
        result.Key.Should().Be($"{jobId}/10/20/foo-bar/transcode");
    }
}
