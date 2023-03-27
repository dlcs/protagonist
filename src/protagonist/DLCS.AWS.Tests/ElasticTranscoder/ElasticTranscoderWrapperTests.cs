using Amazon.ElasticTranscoder;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.S3;
using DLCS.Core.Caching;
using FakeItEasy;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Tests.ElasticTranscoder;

public class ElasticTranscoderWrapperTests
{
    private readonly IAmazonElasticTranscoder elasticTranscoder;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IBucketWriter bucketWriter;
    private readonly IBucketReader bucketReader;
    private readonly ElasticTranscoderWrapper sut;

    public ElasticTranscoderWrapperTests()
    {
        elasticTranscoder = A.Fake<IAmazonElasticTranscoder>();
        bucketWriter = A.Fake<IBucketWriter>();
        bucketReader = A.Fake<IBucketReader>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        
        var cacheSettings = Options.Create(new CacheSettings());
        sut = new ElasticTranscoderWrapper(elasticTranscoder, new MockCachingService(), bucketWriter, bucketReader,
            storageKeyGenerator, cacheSettings, new NullLogger<ElasticTranscoderWrapper>());
    }

    [Fact]
    public async Task CreateJob_CreatesETJob_WithCorrectInput()
    {
        // Arrange
        const string inputKey = "s3://my-test-bucket/the-input/key";
        var expectedInput = new JobInput
        {
            AspectRatio = "auto",
            Container = "auto",
            FrameRate = "auto",
            Interlaced = "auto",
            Resolution = "auto",
            Key = "the-input/key"
        };

        CreateJobRequest? createRequest = null;
        A.CallTo(() => elasticTranscoder.CreateJobAsync(A<CreateJobRequest>._, A<CancellationToken>._))
            .Invokes((CreateJobRequest request, CancellationToken _) =>
            {
                createRequest = request;
            });

        // Act
        await sut.CreateJob(inputKey, string.Empty, new List<CreateJobOutput>(), new Dictionary<string, string>(),
            CancellationToken.None);
        
        // Assert
        createRequest.Input.Should().BeEquivalentTo(expectedInput);
    }
}