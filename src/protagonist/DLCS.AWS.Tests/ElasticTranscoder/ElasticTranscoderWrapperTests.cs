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
        
        A.CallTo(() => elasticTranscoder.ListPresetsAsync(A<ListPresetsRequest>._, A<CancellationToken>._)).Returns(new ListPresetsResponse()
        {
            Presets = new List<Preset>()
            {
                new()
                {
                    Id = "some-preset",
                    Name = "some-preset-name"
                },
                new()
                {
                    Id = "some-preset2",
                    Name = "some-preset-2-name"
                }
            }
        });
        
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

    [Fact]
    public async Task GetPresetIdLookup_ReturnsPresets_WhenCalled()
    {
        // Arrange and Act
        var presets = await sut.GetPresetIdLookup(default);

        // Assert
        presets.Count.Should().Be(2);
        presets.Should().ContainKey("some-preset-name");
        presets.Should().NotContainKey("random-preset");
    }
    
    [Fact]
    public async Task GetPresetDetails_ReturnsPresetDetails_WhenCalled()
    {
        // Arrange and Act
        var preset = await sut.GetPresetDetails("some-preset-name", default);

        // Assert
        preset.Should().NotBeNull();
        preset.Id.Should().Be("some-preset");
    }
    
    [Fact]
    public async Task GetPresetDetails_ReturnsNull_WhenCalledWithInvalidPreset()
    {
        // Arrange and Act
        var preset = await sut.GetPresetDetails("incorrect-name", default);

        // Assert
        preset.Should().BeNull();
    }
}