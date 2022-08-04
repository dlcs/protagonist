using Amazon.ElasticTranscoder;
using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using FakeItEasy;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Tests.ElasticTranscoder;

public class ElasticTranscoderWrapperTests
{
    private readonly IAmazonElasticTranscoder elasticTranscoder;
    private readonly ElasticTranscoderWrapper sut;

    public ElasticTranscoderWrapperTests()
    {
        elasticTranscoder = A.Fake<IAmazonElasticTranscoder>();
        var cacheSettings = Options.Create(new CacheSettings());
        sut = new ElasticTranscoderWrapper(elasticTranscoder, new MockCachingService(), cacheSettings,
            new NullLogger<ElasticTranscoderWrapper>());
    }

    [Fact]
    public async Task CreateJob_CreatesETJob_WithCorrectInput()
    {
        // Arrange
        const string inputKey = "s3://my-test-bucket/the-input/key";
        var assetId = new AssetId(10, 20, "foo");
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
        await sut.CreateJob(assetId, inputKey, string.Empty, new List<CreateJobOutput>(), "id", CancellationToken.None);
        
        // Assert
        createRequest.Input.Should().BeEquivalentTo(expectedInput);
    }
    
    [Fact]
    public async Task CreateJob_SetsExpectedMetadata()
    {
        // Arrange
        const string inputKey = "s3://my-test-bucket/the-input/key";
        var assetId = new AssetId(10, 20, "foo");

        CreateJobRequest? createRequest = null;
        A.CallTo(() => elasticTranscoder.CreateJobAsync(A<CreateJobRequest>._, A<CancellationToken>._))
            .Invokes((CreateJobRequest request, CancellationToken _) =>
            {
                createRequest = request;
            });

        // Act
        await sut.CreateJob(assetId, inputKey, string.Empty, new List<CreateJobOutput>(), "my-id",
            CancellationToken.None);
        
        // Assert
        createRequest.UserMetadata.Should().ContainKey("dlcsId").WhoseValue.Should().Be("10/20/foo");
        createRequest.UserMetadata.Should().ContainKey("jobId").WhoseValue.Should().Be("my-id");
        createRequest.UserMetadata.Should().ContainKey("startTime").WhoseValue.Should().NotBeNullOrEmpty();
    }
}