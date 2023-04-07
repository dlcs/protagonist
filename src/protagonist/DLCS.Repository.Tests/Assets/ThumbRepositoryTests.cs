using System.Collections.Generic;
using System.Threading;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Repository.Assets;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers;

namespace DLCS.Repository.Tests.Assets;

public class ThumbRepositoryTests
{
    private readonly IBucketReader bucketReader;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly ThumbRepository sut;
    private readonly ObjectInBucket objectInBucket;
    private const string Open = @"{""o"": [[683,1024],[267,400],[133,200],[67,100]],""a"": []}";
    private const string Auth = @"{""o"": [],""a"": [[683,1024],[267,400],[133,200],[67,100]]}";
    
    // Note - this is unachievable but confirms ordering
    private const string Mixed = @"{""o"": [[683,1024],[67,100]],""a"": [[267,400],[133,200]]}";

    public ThumbRepositoryTests()
    {
        bucketReader = A.Fake<IBucketReader>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        sut = new ThumbRepository(bucketReader, storageKeyGenerator, new NullLogger<ThumbRepository>());

        objectInBucket = new ObjectInBucket("bucket", "key");
        A.CallTo(() => storageKeyGenerator.GetThumbsSizesJsonLocation(A<AssetId>._))
            .Returns(objectInBucket);
    }

    [Fact]
    public async Task GetOpenSizes_Null_IfNoSizesJsonFound()
    {
        // Arrange
        A.CallTo(() => bucketReader.GetObjectFromBucket(objectInBucket, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(objectInBucket, null, null));
        
        // Act
        var result = await sut.GetOpenSizes(new AssetId(1, 10, "foo"));
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task GetOpenSizes_ReturnsOpenSizes()
    {
        // Arrange
        A.CallTo(() => bucketReader.GetObjectFromBucket(objectInBucket, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(objectInBucket, Open.ToMemoryStream(), null));
        var expected = new List<int[]>
        {
            new[] { 683, 1024 }, new[] { 267, 400 }, new[] { 133, 200 }, new[] { 67, 100 }
        };
        
        // Act
        var result = await sut.GetOpenSizes(new AssetId(1, 10, "foo"));
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task GetOpenSizes_ReturnsOpenSizes_IfMixed()
    {
        // Arrange
        A.CallTo(() => bucketReader.GetObjectFromBucket(objectInBucket, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(objectInBucket, Mixed.ToMemoryStream(), null));
        var expected = new List<int[]> { new[] { 683, 1024 }, new[] { 67, 100 } };
        
        // Act
        var result = await sut.GetOpenSizes(new AssetId(1, 10, "foo"));
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task GetOpenSizes_ReturnsEmpty_IfNoOpen()
    {
        // Arrange
        A.CallTo(() => bucketReader.GetObjectFromBucket(objectInBucket, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(objectInBucket, Auth.ToMemoryStream(), null));
        
        // Act
        var result = await sut.GetOpenSizes(new AssetId(1, 10, "foo"));
        
        // Assert
        result.Should().BeEmpty();
    }
    
    [Fact]
    public async Task GetAllSizes_Null_IfNoSizesJsonFound()
    {
        // Arrange
        A.CallTo(() => bucketReader.GetObjectFromBucket(objectInBucket, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(objectInBucket, null, null));
        
        // Act
        var result = await sut.GetAllSizes(new AssetId(1, 10, "foo"));
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task GetAllSizes_ReturnsAllSizes_AllOpen()
    {
        // Arrange
        A.CallTo(() => bucketReader.GetObjectFromBucket(objectInBucket, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(objectInBucket, Open.ToMemoryStream(), null));
        var expected = new List<int[]>
        {
            new[] { 683, 1024 }, new[] { 267, 400 }, new[] { 133, 200 }, new[] { 67, 100 }
        };
        
        // Act
        var result = await sut.GetAllSizes(new AssetId(1, 10, "foo"));
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task GetAllSizes_ReturnsAllSizesOrdered_Mixed()
    {
        // Arrange
        A.CallTo(() => bucketReader.GetObjectFromBucket(objectInBucket, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(objectInBucket, Mixed.ToMemoryStream(), null));
        var expected = new List<int[]>
        {
            new[] { 683, 1024 }, new[] { 267, 400 }, new[] { 133, 200 }, new[] { 67, 100 }
        };
        
        // Act
        var result = await sut.GetAllSizes(new AssetId(1, 10, "foo"));
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task GetAllSizes_ReturnsAllSizesOrdered_AllAuth()
    {
        // Arrange
        A.CallTo(() => bucketReader.GetObjectFromBucket(objectInBucket, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(objectInBucket, Auth.ToMemoryStream(), null));
        var expected = new List<int[]>
        {
            new[] { 683, 1024 }, new[] { 267, 400 }, new[] { 133, 200 }, new[] { 67, 100 }
        };
        
        // Act
        var result = await sut.GetAllSizes(new AssetId(1, 10, "foo"));
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }
}