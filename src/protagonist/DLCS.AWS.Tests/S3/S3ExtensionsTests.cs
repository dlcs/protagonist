using Amazon.S3.Model;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;

namespace DLCS.AWS.Tests.S3;

public class S3ExtensionsTests
{
    [Fact]
    public void AsObjectInBucket_Correct()
    {
        // Arrange
        var objectInBucket = new ObjectInBucket("test-bucket", "test-key");
        var getObjectResponse = new GetObjectResponse
        {
            Headers =
            {
                CacheControl = "no-cache",
                ContentDisposition = "inline",
                ContentEncoding = "gzip",
                ContentLength = 132123,
                ContentType = "application/json",
                ExpiresUtc = DateTime.UtcNow,
                ContentMD5 = "md5",
            },
            ETag = "my-e-tag",
            LastModified = DateTime.UtcNow.AddDays(-10)
        };

        // Act 
        var objectFromBucket = getObjectResponse.AsObjectInBucket(objectInBucket);
        
        // Assert
        objectFromBucket.ObjectInBucket.Should().Be(objectInBucket);
        objectFromBucket.Headers.CacheControl.Should().Be(getObjectResponse.Headers.CacheControl);
        objectFromBucket.Headers.ContentDisposition.Should().Be(getObjectResponse.Headers.ContentDisposition);
        objectFromBucket.Headers.ContentEncoding.Should().Be(getObjectResponse.Headers.ContentEncoding);
        objectFromBucket.Headers.ContentLength.Should().Be(getObjectResponse.Headers.ContentLength);
        objectFromBucket.Headers.ContentType.Should().Be(getObjectResponse.Headers.ContentType);
        objectFromBucket.Headers.ExpiresUtc.Should().Be(getObjectResponse.Headers.ExpiresUtc);
        objectFromBucket.Headers.ContentMD5.Should().Be(getObjectResponse.Headers.ContentMD5);
        objectFromBucket.Headers.ETag.Should().Be(getObjectResponse.ETag);
        objectFromBucket.Headers.LastModified.Should().Be(getObjectResponse.LastModified);
    }
}