using Amazon.S3;
using Amazon.S3.Model;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace DLCS.AWS.Tests.S3;

public class S3BucketWriterTests
{
    private readonly IAmazonS3 s3Client;
    private readonly S3BucketWriter sut;

    public S3BucketWriterTests()
    {
        s3Client = A.Fake<IAmazonS3>();
        sut = new S3BucketWriter(s3Client, new NullLogger<S3BucketWriter>());
    }

    [Theory]
    [InlineData(10L * 1024 * 1024, 2)] // 10 MB, 2 × 5MB parts (minimum part size)
    [InlineData(50L * 1024 * 1024 * 1024, 10000)] // 50 GB, 10000 parts (boundary: just forces scale-up)
    [InlineData(100L * 1024 * 1024 * 1024, 10000)] // 100 GB, 10000 parts (~10MB each)
    [InlineData(500L * 1024 * 1024 * 1024, 10000)] // 500 GB, 10000 parts (~50MB each)
    public async Task CopyLargeObject_NeverExceeds10000Parts(long fileSize, int expectedPartCount)
    {
        SetupS3ForCopy(fileSize);
        var source = new ObjectInBucket("src-bucket", "src-key");
        var destination = new ObjectInBucket("dst-bucket", "dst-key");

        var result = await sut.CopyLargeObject(source, destination);

        result.Result.Should().Be(LargeObjectStatus.Success);

        A.CallTo(() => s3Client.CopyPartAsync(A<CopyPartRequest>.Ignored, A<CancellationToken>.Ignored))
            .MustHaveHappened(expectedPartCount, Times.Exactly);
        A.CallTo(() => s3Client.CopyPartAsync(
                A<CopyPartRequest>.That.Matches(r => r.PartNumber > 10000), A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task CopyLargeObject_Uses5MBParts_ForSmallFile()
    {
        const long fileSize = 10L * 1024 * 1024; // 10 MB, 2 parts at 5 MB each
        SetupS3ForCopy(fileSize);
        var source = new ObjectInBucket("src-bucket", "src-key");
        var destination = new ObjectInBucket("dst-bucket", "dst-key");

        var result = await sut.CopyLargeObject(source, destination);

        result.Result.Should().Be(LargeObjectStatus.Success);

        var partRequests = Fake.GetCalls(s3Client)
            .Where(c => c.Method.Name == nameof(IAmazonS3.CopyPartAsync))
            .Select(c => (CopyPartRequest)c.Arguments[0]!)
            .ToList();

        partRequests.Should().HaveCount(2);
        (partRequests[0]!.LastByte - partRequests[0]!.FirstByte + 1).Should().Be(5 * 1024 * 1024);
    }
    
    private void SetupS3ForCopy(long fileSize, string uploadId = "upload-id")
    {
        A.CallTo(() => s3Client.GetObjectMetadataAsync(
                A<GetObjectMetadataRequest>.Ignored, A<CancellationToken>.Ignored))
            .Returns(new GetObjectMetadataResponse { ContentLength = fileSize });

        A.CallTo(() => s3Client.InitiateMultipartUploadAsync(
                A<InitiateMultipartUploadRequest>.Ignored, A<CancellationToken>.Ignored))
            .Returns(new InitiateMultipartUploadResponse { UploadId = uploadId });

        A.CallTo(() => s3Client.CopyPartAsync(
                A<CopyPartRequest>.Ignored, A<CancellationToken>.Ignored))
            .Returns(new CopyPartResponse());

        A.CallTo(() => s3Client.CompleteMultipartUploadAsync(
                A<CompleteMultipartUploadRequest>.Ignored, A<CancellationToken>.Ignored))
            .Returns(new CompleteMultipartUploadResponse());
    }
}
