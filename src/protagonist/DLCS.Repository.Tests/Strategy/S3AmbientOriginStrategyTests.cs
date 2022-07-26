using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Customers;
using DLCS.Model.Storage;
using DLCS.Repository.Strategy;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers;
using Xunit;

namespace DLCS.Repository.Tests.Strategy;

public class S3AmbientOriginStrategyTests
{
    private readonly S3AmbientOriginStrategy sut;
    private readonly IBucketReader bucketReader;
    private readonly CustomerOriginStrategy customerOriginStrategy;
    private readonly AssetId assetId = new(2, 2, "foo");

    public S3AmbientOriginStrategyTests()
    {
        bucketReader = A.Fake<IBucketReader>();

        sut = new S3AmbientOriginStrategy(bucketReader, new NullLogger<S3AmbientOriginStrategy>());
        customerOriginStrategy = new CustomerOriginStrategy
        {
            Strategy = OriginStrategyType.S3Ambient
        };
    }

    [Fact]
    public async Task LoadAssetFromOrigin_ReturnsExpectedResponse_OnSuccess()
    {
        // Arrange
        const string contentType = "application/json";
        const long contentLength = 4324;
        var response = new ObjectFromBucket(new ObjectInBucket("bucket"),
            "this is a test".ToMemoryStream(),
            new ObjectInBucketHeaders
            {
                ContentType = contentType,
                ContentLength = contentLength
            }
        );
        
        const string originUri = "s3://eu-west-1/test-storage/2/1/ratts-of-the-capital";
        var objectInBucket =
            new RegionalisedObjectInBucket("test-storage", "2/1/ratts-of-the-capital", "eu-west-1");
        var regionalisedString = objectInBucket.ToString();
        A.CallTo(() =>
                bucketReader.GetObjectFromBucket(
                    A<ObjectInBucket>.That.Matches(a => a.ToString() == regionalisedString),
                    A<CancellationToken>._))
            .Returns(response);

        // Act
        var result = await sut.LoadAssetFromOrigin(assetId, originUri, customerOriginStrategy);
        
        // Assert
        A.CallTo(() =>
                bucketReader.GetObjectFromBucket(
                    A<ObjectInBucket>.That.Matches(a => a.ToString() == regionalisedString),
                    A<CancellationToken>._))
            .MustHaveHappened();
        result.Stream.Should().NotBeNull().And.Subject.Should().NotBeSameAs(Stream.Null);
        result.ContentLength.Should().Be(contentLength);
        result.ContentType.Should().Be(contentType);
    }
    
    [Fact]
    public async Task LoadAssetFromOrigin_HandlesNoContentLengthAndType()
    {
        // Arrange
        var response = new ObjectFromBucket(new ObjectInBucket("bucket"),
            "this is a test".ToMemoryStream(),
            new ObjectInBucketHeaders()
        );
        
        const string originUri = "s3://eu-west-1/test-storage/2/1/repelish";
        A.CallTo(() => bucketReader.GetObjectFromBucket(A<ObjectInBucket>._, A<CancellationToken>._))
            .Returns(response);

        // Act
        var result = await sut.LoadAssetFromOrigin(assetId, originUri, customerOriginStrategy);
        
        // Assert
        result.Stream.Should().NotBeNull().And.Subject.Should().NotBeSameAs(Stream.Null);
        result.ContentLength.Should().BeNull();
        result.ContentType.Should().BeNull();
    }
    
    [Fact]
    public async Task LoadAssetFromOrigin_ReturnsNull_IfCallFails()
    {
        // Arrange
        const string originUri = "s3://eu-west-1/test-storage/2/1/repelish";
        A.CallTo(() => bucketReader.GetObjectFromBucket(A<ObjectInBucket>._, A<CancellationToken>._))
            .ThrowsAsync(new Exception());
        
        // Act
        var result = await sut.LoadAssetFromOrigin(assetId, originUri, customerOriginStrategy);
        
        // Assert
        result.Stream.Should().BeSameAs(Stream.Null);
        result.IsEmpty.Should().BeTrue();
    }
}