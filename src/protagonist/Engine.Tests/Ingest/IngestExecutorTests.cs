using DLCS.Model.Assets;
using DLCS.Model.Customers;
using Engine.Data;
using Engine.Ingest;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Engine.Tests.Ingest;

public class IngestExecutorTests
{
    private readonly IWorkerBuilder workerBuilder;
    private readonly IEngineAssetRepository repo;
    private readonly IngestExecutor sut;
    private readonly CustomerOriginStrategy customerOriginStrategy = new();

    public IngestExecutorTests()
    {
        workerBuilder = A.Fake<IWorkerBuilder>();
        repo = A.Fake<IEngineAssetRepository>();
        sut = new IngestExecutor(workerBuilder, repo, new NullLogger<IngestExecutor>());
    }

    [Theory]
    [InlineData(IngestResultStatus.Success, IngestResultStatus.Success, IngestResultStatus.Success)]
    [InlineData(IngestResultStatus.Success, IngestResultStatus.QueuedForProcessing,
        IngestResultStatus.QueuedForProcessing)]
    [InlineData(IngestResultStatus.QueuedForProcessing, IngestResultStatus.Success,
        IngestResultStatus.QueuedForProcessing)]
    public async Task IngestAsset_Success_ReturnsCorrectStatus(IngestResultStatus first, IngestResultStatus second,
        IngestResultStatus overall)
    {
        // Arrange
        var asset = new Asset();
        A.CallTo(() => workerBuilder.GetWorkers(asset))
            .Returns(new[] { new FakeWorker(first), new FakeWorker(second) });

        A.CallTo(() => repo.UpdateIngestedAsset(asset, A<ImageLocation?>._, A<ImageStorage?>._, A<CancellationToken>._))
            .Returns(true);

        // Act
        var result = await sut.IngestAsset(asset, customerOriginStrategy);

        // Assert
        result.Status.Should().Be(overall);
    }
    
    [Theory]
    [InlineData(IngestResultStatus.Failed)]
    [InlineData(IngestResultStatus.Success)]
    [InlineData(IngestResultStatus.QueuedForProcessing)]
    [InlineData(IngestResultStatus.StorageLimitExceeded)]
    public async Task IngestAsset_ReturnsFailure_IfDbSaveFails_RegardlessOfWorkerState(IngestResultStatus status)
    {
        // Arrange
        var asset = new Asset();
        A.CallTo(() => workerBuilder.GetWorkers(asset))
            .Returns(new[] { new FakeWorker(status) });

        A.CallTo(() => repo.UpdateIngestedAsset(asset, A<ImageLocation?>._, A<ImageStorage?>._, A<CancellationToken>._))
            .Returns(false);

        // Act
        var result = await sut.IngestAsset(asset, customerOriginStrategy);

        // Assert
        A.CallTo(() => repo.UpdateIngestedAsset(asset, A<ImageLocation?>._, A<ImageStorage?>._, A<CancellationToken>._))
            .MustHaveHappened();
        result.Status.Should().Be(IngestResultStatus.Failed);
    }
    
    [Fact]
    public async Task IngestAsset_CallsPostProcessorsForAllThatHaveBeenProcessed_RegardlessOfInitialStatus()
    {
        // Arrange
        var asset = new Asset();
        var first = new FakeWorkerWithPost(IngestResultStatus.Success);
        var second = new FakeWorkerWithPost(IngestResultStatus.Failed);
        var third = new FakeWorkerWithPost(IngestResultStatus.Success);
        A.CallTo(() => workerBuilder.GetWorkers(asset))
            .Returns(new[] { first, second, third });

        A.CallTo(() => repo.UpdateIngestedAsset(asset, A<ImageLocation?>._, A<ImageStorage?>._, A<CancellationToken>._))
            .Returns(false);

        // Act
        var result = await sut.IngestAsset(asset, customerOriginStrategy);

        // Assert
        A.CallTo(() => repo.UpdateIngestedAsset(asset, A<ImageLocation?>._, A<ImageStorage?>._, A<CancellationToken>._))
            .MustHaveHappened();
        result.Status.Should().Be(IngestResultStatus.Failed);
        first.Called.Should().BeTrue();
        first.PostCalled.Should().BeTrue();
        second.Called.Should().BeTrue();
        second.PostCalled.Should().BeTrue();
        third.Called.Should().BeFalse();
        third.PostCalled.Should().BeFalse();
    }

    [Theory]
    [InlineData(IngestResultStatus.Failed)]
    [InlineData(IngestResultStatus.StorageLimitExceeded)]
    public async Task IngestAsset_FirstWorkerFail_DoesNotCallFurtherWorkers(IngestResultStatus status)
    {
        // Arrange
        var asset = new Asset();

        var secondWorker = new FakeWorker(IngestResultStatus.Success);
        
        A.CallTo(() => workerBuilder.GetWorkers(asset)).Returns(new[] { new FakeWorker(status), secondWorker });

        A.CallTo(() => repo.UpdateIngestedAsset(asset, A<ImageLocation?>._, A<ImageStorage?>._, A<CancellationToken>._))
            .Returns(true);

        // Act
        var result = await sut.IngestAsset(asset, customerOriginStrategy);

        // Assert
        result.Status.Should().Be(status);
        secondWorker.Called.Should().BeFalse();
    }
}

public class FakeWorker : IAssetIngesterWorker
{
    private readonly IngestResultStatus result;
    public bool Called { get; private set; }
    
    public FakeWorker(IngestResultStatus result)
    {
        this.result = result;
    }

    public Task<IngestResultStatus> Ingest(IngestionContext ingestionContext,
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
        Called = true;
        return Task.FromResult(result);
    }
}

public class FakeWorkerWithPost : IAssetIngesterWorker, IAssetIngesterPostProcess
{
    private readonly IngestResultStatus result;
    public bool Called { get; private set; }
    public bool PostCalled { get; private set; }

    public FakeWorkerWithPost(IngestResultStatus result)
    {
        this.result = result;
    }

    public Task<IngestResultStatus> Ingest(IngestionContext ingestionContext,
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
        Called = true;
        return Task.FromResult(result);
    }

    public Task PostIngest(IngestionContext ingestionContext, bool ingestSuccessful)
    {
        PostCalled = true;
        return Task.CompletedTask;
    }
}