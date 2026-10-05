using System.Net;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.AWS.Configuration;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Exceptions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace DLCS.AWS.Tests.S3;

public class BucketReaderTests
{
    private readonly IAmazonS3 s3Client;
    private readonly S3BucketReader sut;
    
    public BucketReaderTests()
    {
        s3Client = A.Fake<IAmazonS3>();
        sut = new S3BucketReader(new AmbientAwsClientProvider<IAmazonS3>(s3Client), new NullLogger<S3BucketReader>());
    }

    [Fact]
    public async Task GetObjectFromBucket_ReturnsFoundObjectAsStream()
    {
        // Arrange
        const string bucket = "MyBucket";
        const string key = "MyKey";
        const string bucketResponse = "This is a response from s3";
        
        var responseStream = new MemoryStream(Encoding.Default.GetBytes(bucketResponse));
        A.CallTo(() =>
                s3Client.GetObjectAsync(
                    A<GetObjectRequest>.That.Matches(r => r.BucketName == bucket && r.Key == key),
                    A<CancellationToken>.Ignored))
            .Returns(new GetObjectResponse {ResponseStream = responseStream});

        var objectInBucket = new ObjectInBucket(bucket, key);

        // Act
        var targetStream = (await sut.GetObjectFromBucket(objectInBucket)).Stream;

        // Assert
        var memoryStream = new MemoryStream();
        await targetStream!.CopyToAsync(memoryStream);
        
        var actual = Encoding.Default.GetString(memoryStream.ToArray());
        actual.Should().Be(bucketResponse);
    }
    
    [Fact]
    public async Task GetObjectFromBucket_ReturnsNullStream_IfKeyNotFound()
    {
        // Arrange
        A.CallTo(() =>
                s3Client.GetObjectAsync(
                    A<GetObjectRequest>.Ignored,
                    A<CancellationToken>.Ignored))
            .ThrowsAsync(new AmazonS3Exception("uh-oh", ErrorType.Unknown, "123", "xxx-1", HttpStatusCode.NotFound));

        var objectInBucket = new ObjectInBucket("MyBucket", "MyKey");

        // Act
        var result = await sut.GetObjectFromBucket(objectInBucket);

        // Assert
        result.Stream.Should().BeSameAs(Stream.Null);
    }

    [Theory]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetObjectFromBucket_ThrowsHttpException_IfS3CopyFails_DueToNon404(HttpStatusCode statusCode)
    {
        // Arrange
        A.CallTo(() =>
                s3Client.GetObjectAsync(
                    A<GetObjectRequest>.Ignored,
                    A<CancellationToken>.Ignored))
            .ThrowsAsync(new AmazonS3Exception("uh-oh", ErrorType.Unknown, "123", "xxx-1", statusCode));

        var objectInBucket = new ObjectInBucket("MyBucket", "MyKey");

        // Act
        Func<Task> action = () => sut.GetObjectFromBucket(objectInBucket);

        // Assert
        (await action.Should().ThrowAsync<HttpException>()).Which.StatusCode.Should().Be(statusCode);
    }

    [Fact]
    public async Task GetObjectHeaders_ReturnsHeadersWithContentLength_WhenObjectFound()
    {
        // Arrange
        const string bucket = "MyBucket";
        const string key = "MyKey";
        const long contentLength = 1234L;

        var response = new GetObjectMetadataResponse();
        response.Headers.ContentLength = contentLength;
        response.Headers.ContentType = "image/jpeg";

        A.CallTo(() =>
                s3Client.GetObjectMetadataAsync(
                    A<GetObjectMetadataRequest>.That.Matches(r => r.BucketName == bucket && r.Key == key),
                    A<CancellationToken>.Ignored))
            .Returns(response);

        var objectInBucket = new ObjectInBucket(bucket, key);

        // Act
        var result = await sut.GetObjectHeaders(objectInBucket);

        // Assert
        result.Should().NotBeNull();
        result!.ContentLength.Should().Be(contentLength);
        result.ContentType.Should().Be("image/jpeg");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetObjectHeaders_ReturnsNull_IfKeyNotFound(bool throwOnError)
    {
        // Arrange
        A.CallTo(() =>
                s3Client.GetObjectMetadataAsync(
                    A<GetObjectMetadataRequest>.Ignored,
                    A<CancellationToken>.Ignored))
            .ThrowsAsync(new AmazonS3Exception("uh-oh", ErrorType.Unknown, "123", "xxx-1", HttpStatusCode.NotFound));

        var objectInBucket = new ObjectInBucket("MyBucket", "MyKey");

        // Act
        var result = await sut.GetObjectHeaders(objectInBucket, throwOnError);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetObjectHeaders_ThrowsHttpException_DueToNon404_IfThrowOnErrorTrue(HttpStatusCode statusCode)
    {
        // Arrange
        A.CallTo(() =>
                s3Client.GetObjectMetadataAsync(
                    A<GetObjectMetadataRequest>.Ignored,
                    A<CancellationToken>.Ignored))
            .ThrowsAsync(new AmazonS3Exception("uh-oh", ErrorType.Unknown, "123", "xxx-1", statusCode));

        var objectInBucket = new ObjectInBucket("MyBucket", "MyKey");

        // Act
        Func<Task> action = () => sut.GetObjectHeaders(objectInBucket, throwOnError: true);

        // Assert
        (await action.Should().ThrowAsync<HttpException>()).Which.StatusCode.Should().Be(statusCode);
    }
    
    [Theory]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetObjectHeaders_ReturnsNull_DueToNon404_IfThrowOnErrorFalse(HttpStatusCode statusCode)
    {
        // Arrange
        A.CallTo(() =>
                s3Client.GetObjectMetadataAsync(
                    A<GetObjectMetadataRequest>.Ignored,
                    A<CancellationToken>.Ignored))
            .ThrowsAsync(new AmazonS3Exception("uh-oh", ErrorType.Unknown, "123", "xxx-1", statusCode));

        var objectInBucket = new ObjectInBucket("MyBucket", "MyKey");

        // Act
        var result = await sut.GetObjectHeaders(objectInBucket);

        // Assert
        result.Should().BeNull("throwOnError=false should return null on err");
    }
}
