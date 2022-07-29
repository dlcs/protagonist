using DLCS.AWS.S3;
using DLCS.Model.Assets;
using Engine.Ingest.Completion;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Engine.Tests.Ingest.Completion;

public class TimebasedIngesterCompletionTests
{
    private readonly TimebasedIngestorCompletion sut;
    private readonly IEngineAssetRepository engineAssetRepository;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IBucketWriter bucketWriter;

    public TimebasedIngesterCompletionTests()
    {
        engineAssetRepository = A.Fake<IEngineAssetRepository>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        bucketWriter = A.Fake<IBucketWriter>();
        sut = new TimebasedIngestorCompletion(engineAssetRepository, storageKeyGenerator, bucketWriter,
            NullLogger<TimebasedIngestorCompletion>.Instance);
    }

    [Fact]
    public async Task CompleteAssetInDatabase_DoesNotCreateLocationOrStorage_IfNoSize()
    {
        // Arrange
        var asset = new Asset { Id = "10/20/foo" };
        var token = new CancellationToken();

        // Act
        await sut.CompleteAssetInDatabase(asset, cancellationToken: token);

        // Assert
        A.CallTo(() => engineAssetRepository.UpdateIngestedAsset(asset, null, null, token))
            .MustHaveHappened();
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompleteAssetInDatabase_ReturnsResultOfRepositoryCall(bool result)
    {
        // Arrange
        var asset = new Asset { Id = "10/20/foo" };
        var token = new CancellationToken();

        A.CallTo(() => engineAssetRepository.UpdateIngestedAsset(asset, null, null, token)).Returns(result);

        // Act
        var success = await sut.CompleteAssetInDatabase(asset, cancellationToken: token);

        // Assert
        success.Should().Be(result);
    }
    
    [Fact]
    public async Task CompleteAssetInDatabase_CreatesLocationAndStorage_IfSize()
    {
        // Arrange
        const string assetId = "10/20/foo";
        var asset = new Asset { Id = assetId, Customer = 10, Space = 20 };
        var size = 1967L;
        var token = new CancellationToken();

        // Act
        await sut.CompleteAssetInDatabase(asset, size, cancellationToken: token);

        // Assert
        A.CallTo(() => engineAssetRepository.UpdateIngestedAsset(asset,
                A<ImageLocation>.That.Matches(al =>
                    al.Id == assetId && al.Nas == string.Empty && al.S3 == string.Empty),
                A<ImageStorage>.That.Matches(
                    s => s.Id == assetId && s.Size == size && s.Customer == 10 && s.Space == 20),
                token))
            .MustHaveHappened();
    }
}