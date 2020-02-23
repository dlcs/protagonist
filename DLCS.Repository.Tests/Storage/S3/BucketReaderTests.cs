using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Core.Exceptions;
using DLCS.Model.Storage;
using DLCS.Repository.Storage.S3;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DLCS.Repository.Tests.Storage.S3
{
    public class BucketReaderTests
    {
        private readonly IAmazonS3 s3Client;
        private readonly ILogger<BucketReader> logger;
        private readonly BucketReader sut;
        
        public BucketReaderTests()
        {
            s3Client = A.Fake<IAmazonS3>();
            logger = A.Fake<ILogger<BucketReader>>();
            sut = new BucketReader(s3Client, logger);
        }

        [Fact]
        public async Task WriteObjectFromBucket_WritesResponseStreamToProvidedStream()
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

            var objectInBucket = new ObjectInBucket {Bucket = bucket, Key = key};
            var targetStream = new MemoryStream();
            
            // Act
            await sut.WriteObjectFromBucket(objectInBucket, targetStream);

            // Assert
            var actual = Encoding.Default.GetString(targetStream.ToArray());
            actual.Should().Be(bucketResponse);
        }

        [Theory]
        [InlineData(HttpStatusCode.Redirect)]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public void WriteObjectFromBucket_ThrowsHttpException_IfS3CopyFails(HttpStatusCode statusCode)
        {
            // Arrange
            A.CallTo(() =>
                    s3Client.GetObjectAsync(
                        A<GetObjectRequest>.Ignored,
                        A<CancellationToken>.Ignored))
                .ThrowsAsync(new AmazonS3Exception("uh-oh", ErrorType.Unknown, "123", "xxx-1", statusCode));

            var objectInBucket = new ObjectInBucket {Bucket = "MyBucket", Key = "MyKey"};

            // Act
            Func<Task> action = () => sut.WriteObjectFromBucket(objectInBucket, new MemoryStream());

            // Assert
            action.Should().Throw<HttpException>().Which.StatusCode.Should().Be(statusCode);
        }
    }
}