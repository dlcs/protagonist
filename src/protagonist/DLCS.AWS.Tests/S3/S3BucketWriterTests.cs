using Amazon.S3;
using Amazon.S3.Model;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Tests.S3;

public class S3BucketWriterTests
{
    private readonly IAmazonS3 s3Client;
    private readonly S3BucketWriter sut;

    public S3BucketWriterTests()
    {
        s3Client = A.Fake<IAmazonS3>();
        var awsOptions = Options.Create(new AWSSettings { S3 = new S3Settings { CopyPartConcurrency = 4 } });
        sut = new S3BucketWriter(s3Client, awsOptions, new NullLogger<S3BucketWriter>());
    }

    [Theory]
    [InlineData(32L * 1024 * 1024, 2)] // 32 MB , 2 × 16MB parts (minimum part size)
    [InlineData(50L * 1024 * 1024 * 1024, 3200)]  // 50 GB , 3200 × 16MB parts
    [InlineData(100L * 1024 * 1024 * 1024, 6400)] // 100 GB, 6400 × 16MB parts
    [InlineData(500L * 1024 * 1024 * 1024, 10000)] // 500 GB, 10000 parts (scale-up kicks in ~53MB each)
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
    public async Task CopyLargeObject_Uses16MBParts_ForSmallFile()
    {
        const long fileSize = 32L * 1024 * 1024; // 32 MB, 2 × 16MB parts
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
        (partRequests[0]!.LastByte - partRequests[0]!.FirstByte + 1).Should().Be(16 * 1024 * 1024);
    }

    [Fact]
    public async Task CopyLargeObject_CopiesPartsInParallel()
    {
        const long fileSize = 64L * 1024 * 1024; // 64 MB, 4 × 16MB parts, matches CopyPartConcurrency of 4
        SetupS3ForCopy(fileSize);
        var source = new ObjectInBucket("src-bucket", "src-key");
        var destination = new ObjectInBucket("dst-bucket", "dst-key");

        var currentConcurrent = 0;
        var maxConcurrent = 0;

        A.CallTo(() => s3Client.CopyPartAsync(A<CopyPartRequest>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsLazily(async _ =>
            {
                var current = Interlocked.Increment(ref currentConcurrent);
                InterlockedMax(ref maxConcurrent, current);
                await Task.Delay(50);
                Interlocked.Decrement(ref currentConcurrent);
                return new CopyPartResponse();
            });

        var result = await sut.CopyLargeObject(source, destination);

        result.Result.Should().Be(LargeObjectStatus.Success);
        maxConcurrent.Should().BeGreaterThan(1, "parts should be copied in parallel");
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

    // Thread-safe max update — Interlocked.Max is .NET 9+, so use a compare-and-swap loop on .NET 8
    private static void InterlockedMax(ref int location, int candidate)
    {
        int current;
        do
        {
            current = Volatile.Read(ref location);
            if (candidate <= current) return;
        } while (Interlocked.CompareExchange(ref location, candidate, current) != current);
    }
}
