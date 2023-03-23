using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder.Models;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Engine.Data;
using Engine.Ingest.Timebased.Completion;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Engine.Tests.Ingest.Timebased.Completion;

public class TimebasedIngesterCompletionTests
{
    private readonly IEngineAssetRepository engineAssetRepository;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IBucketWriter bucketWriter;

    public TimebasedIngesterCompletionTests()
    {
        engineAssetRepository = A.Fake<IEngineAssetRepository>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        bucketWriter = A.Fake<IBucketWriter>();
    }

    private TimebasedIngestorCompletion GetSut()
    {
        return new TimebasedIngestorCompletion(engineAssetRepository, storageKeyGenerator, bucketWriter,
            NullLogger<TimebasedIngestorCompletion>.Instance);
    }

    [Fact]
    public async Task CompleteAssetInDatabase_DoesNotCreateLocationOrStorage_IfNoSize()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("10/20/foo"));
        var token = new CancellationToken();

        // Act
        var sut = GetSut();
        await sut.CompleteAssetInDatabase(asset, cancellationToken: token);

        // Assert
        A.CallTo(() => engineAssetRepository.UpdateIngestedAsset(asset, null, null, true, token))
            .MustHaveHappened();
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompleteAssetInDatabase_ReturnsResultOfRepositoryCall(bool result)
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("10/20/foo"));
        var token = new CancellationToken();

        A.CallTo(() => engineAssetRepository.UpdateIngestedAsset(asset, null, null, true, token)).Returns(result);

        // Act
        var sut = GetSut();
        var success = await sut.CompleteAssetInDatabase(asset, cancellationToken: token);

        // Assert
        success.Should().Be(result);
    }
    
    [Fact]
    public async Task CompleteAssetInDatabase_CreatesLocationAndStorage_IfSize()
    {
        // Arrange
        var assetId = AssetId.FromString("10/20/foo");
        var asset = new Asset(assetId);
        var size = 1967L;
        var token = new CancellationToken();

        // Act
        var sut = GetSut();
        await sut.CompleteAssetInDatabase(asset, size, cancellationToken: token);

        // Assert
        A.CallTo(() => engineAssetRepository.UpdateIngestedAsset(asset,
                A<ImageLocation>.That.Matches(al =>
                    al.Id == assetId && al.Nas == string.Empty && al.S3 == string.Empty),
                A<ImageStorage>.That.Matches(
                    s => s.Id == assetId && s.Size == size && s.Customer == 10 && s.Space == 20),
                true,
                token))
            .MustHaveHappened();
    }

    [Fact]
    public async Task CompleteSuccessfulIngest_False_IfAssetNotFound()
    {
        // Arrange
        var assetId = new AssetId(10, 9, "endtroducing");
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, A<CancellationToken>._)).Returns<Asset?>(null);
        
        // Act
        var sut = GetSut();
        var result = await sut.CompleteSuccessfulIngest(assetId, new TranscodeResult());
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task CompleteSuccessfulIngest_FalseAndErrorSet_IfTranscodeNotComplete()
    {
        // Arrange
        var assetId = new AssetId(10, 9, "endtroducing");
        var asset = new Asset(assetId);
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, A<CancellationToken>._)).Returns(asset);
        
        // Act
        var sut = GetSut();
        var result = await sut.CompleteSuccessfulIngest(assetId,
            new TranscodeResult(new TranscodedNotification
                { Input = new JobInput(), State = "ERROR", Outputs = new List<TranscodeOutput>() }));
        
        // Assert
        result.Should().BeFalse();
        asset.Error.Should().NotBeNull();
    }
    
    [Theory]
    [InlineData(LargeObjectStatus.Cancelled)]
    [InlineData(LargeObjectStatus.Error)]
    [InlineData(LargeObjectStatus.SourceNotFound)]
    [InlineData(LargeObjectStatus.Unknown)]
    public async Task CompleteSuccessfulIngest_FalseAndErrorSet_IfCopyNotFound(LargeObjectStatus status)
    {
        // Arrange
        var assetId = new AssetId(10, 9, "endtroducing");
        var asset = new Asset(assetId);
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, A<CancellationToken>._)).Returns(asset);
        var transcodeResult = new TranscodeResult(new TranscodedNotification
        {
            Input = new JobInput(),
            State = "COMPLETED",
            Outputs = new List<TranscodeOutput>
            {
                new() { Duration = 100, Width = 123, Height = 234, Key = "path" }
            }
        });
        A.CallTo(() => bucketWriter.CopyLargeObject(A<ObjectInBucket>._, A<ObjectInBucket>._,
                A<Func<long, Task<bool>>>._, A<bool>._, A<string?>._, A<CancellationToken>._))
            .Returns(new LargeObjectCopyResult(status));
        
        // Act
        var sut = GetSut();
        var result = await sut.CompleteSuccessfulIngest(assetId, transcodeResult);

        // Assert
        result.Should().BeFalse();
        asset.Error.Should().NotBeNull();
    }
}