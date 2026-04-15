using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Processing;
using DLCS.Repository.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace DLCS.Repository.Tests.Messaging;

public class IngestNotificationSenderTests
{
    private readonly IEngineClient engineClient;
    private readonly ICustomerQueueRepository customerQueueRepository;
    private readonly IngestNotificationSender sut;

    public IngestNotificationSenderTests()
    {
        engineClient = A.Fake<IEngineClient>();
        customerQueueRepository = A.Fake<ICustomerQueueRepository>();

        sut = new IngestNotificationSender(engineClient, customerQueueRepository,
            new NullLogger<IngestNotificationSender>());
    }

    [Fact]
    public async Task SendImmediateIngestAssetRequest_ReturnsEngineStatusCode()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("99/1/test-asset"));
        A.CallTo(() => engineClient.SynchronousIngest(asset, A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        // Act
        var result = await sut.SendImmediateIngestAssetRequest(asset);

        // Assert
        result.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendImmediateIngestAssetRequest_DoesNotTouchQueue()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("99/1/test-asset"));
        A.CallTo(() => engineClient.SynchronousIngest(asset, A<CancellationToken>._))
            .Returns(HttpStatusCode.OK);

        // Act
        await sut.SendImmediateIngestAssetRequest(asset);

        // Assert
        A.CallTo(() => customerQueueRepository.IncrementSize(A<int>._, A<string>._, A<int>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => customerQueueRepository.DecrementSize(A<int>._, A<string>._, A<int>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task SendIngestAssetRequest_IncrementsDefaultQueue_BeforeCallingEngine()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("99/1/test-asset"));
        var callOrder = new List<string>();

        A.CallTo(() => customerQueueRepository.IncrementSize(99, QueueNames.Default, 1, A<CancellationToken>._))
            .Invokes(_ => callOrder.Add("increment"))
            .Returns(Task.CompletedTask);
        A.CallTo(() => engineClient.AsynchronousIngest(asset, A<CancellationToken>._))
            .Invokes(_ => callOrder.Add("engine"))
            .Returns(true);

        // Act
        await sut.SendIngestAssetRequest(asset);

        // Assert
        callOrder.Should().ContainInOrder("increment", "engine");
    }

    [Fact]
    public async Task SendIngestAssetRequest_ReturnsTrue_WhenEngineSucceeds()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("99/1/test-asset"));
        A.CallTo(() => engineClient.AsynchronousIngest(asset, A<CancellationToken>._)).Returns(true);

        // Act
        var result = await sut.SendIngestAssetRequest(asset);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendIngestAssetRequest_DoesNotDecrement_WhenEngineSucceeds()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("99/1/test-asset"));
        A.CallTo(() => engineClient.AsynchronousIngest(asset, A<CancellationToken>._)).Returns(true);

        // Act
        await sut.SendIngestAssetRequest(asset);

        // Assert
        A.CallTo(() => customerQueueRepository.DecrementSize(A<int>._, A<string>._, A<int>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task SendIngestAssetRequest_ReturnsFalse_WhenEngineFails()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("99/1/test-asset"));
        A.CallTo(() => engineClient.AsynchronousIngest(asset, A<CancellationToken>._)).Returns(false);

        // Act
        var result = await sut.SendIngestAssetRequest(asset);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendIngestAssetRequest_DecrementsDefaultQueue_WhenEngineFails()
    {
        // Arrange
        var asset = new Asset(AssetId.FromString("99/1/test-asset"));
        A.CallTo(() => engineClient.AsynchronousIngest(asset, A<CancellationToken>._)).Returns(false);

        // Act
        await sut.SendIngestAssetRequest(asset);

        // Assert
        A.CallTo(() => customerQueueRepository.DecrementSize(99, QueueNames.Default, 1, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SendIngestAssetsRequest_ReturnsZero_WhenAssetsEmpty()
    {
        // Act
        var result = await sut.SendIngestAssetsRequest([], false);

        // Assert
        result.Should().Be(0);
        A.CallTo(() => customerQueueRepository.IncrementSize(A<int>._, A<string>._, A<int>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => engineClient.AsynchronousIngestBatch(A<IReadOnlyCollection<Asset>>._, A<bool>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task SendIngestAssetsRequest_IncrementsDefaultQueue_WhenNotPriority()
    {
        // Arrange
        var assets = new List<Asset> { new(AssetId.FromString("99/1/asset-1")), new(AssetId.FromString("99/1/asset-2")) };
        A.CallTo(() => engineClient.AsynchronousIngestBatch(A<IReadOnlyCollection<Asset>>._, false, A<CancellationToken>._))
            .Returns(assets.Count);

        // Act
        await sut.SendIngestAssetsRequest(assets, false);

        // Assert
        A.CallTo(() => customerQueueRepository.IncrementSize(99, QueueNames.Default, 2, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SendIngestAssetsRequest_IncrementsPriorityQueue_WhenPriority()
    {
        // Arrange
        var assets = new List<Asset> { new(AssetId.FromString("99/1/asset-1")), new(AssetId.FromString("99/1/asset-2")) };
        A.CallTo(() => engineClient.AsynchronousIngestBatch(A<IReadOnlyCollection<Asset>>._, true, A<CancellationToken>._))
            .Returns(assets.Count);

        // Act
        await sut.SendIngestAssetsRequest(assets, true);

        // Assert
        A.CallTo(() => customerQueueRepository.IncrementSize(99, QueueNames.Priority, 2, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SendIngestAssetsRequest_ReturnsSentCount_WhenAllSucceed()
    {
        // Arrange
        var assets = new List<Asset> { new(AssetId.FromString("99/1/asset-1")), new(AssetId.FromString("99/1/asset-2")) };
        A.CallTo(() => engineClient.AsynchronousIngestBatch(A<IReadOnlyCollection<Asset>>._, false, A<CancellationToken>._))
            .Returns(2);

        // Act
        var result = await sut.SendIngestAssetsRequest(assets, false);

        // Assert
        result.Should().Be(2);
        A.CallTo(() => customerQueueRepository.DecrementSize(A<int>._, A<string>._, A<int>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task SendIngestAssetsRequest_DecrementsQueueByDifference_WhenSomeMessagesFail()
    {
        // Arrange
        var assets = new List<Asset>
        {
            new(AssetId.FromString("99/1/asset-1")),
            new(AssetId.FromString("99/1/asset-2")),
            new(AssetId.FromString("99/1/asset-3"))
        };
        A.CallTo(() => engineClient.AsynchronousIngestBatch(A<IReadOnlyCollection<Asset>>._, false, A<CancellationToken>._))
            .Returns(2); // 1 failed

        // Act
        var result = await sut.SendIngestAssetsRequest(assets, false);

        // Assert
        result.Should().Be(2);
        A.CallTo(() => customerQueueRepository.DecrementSize(99, QueueNames.Default, 1, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SendIngestAdjunctRequest_Single_IncrementsAdjunctQueue()
    {
        // Arrange
        var adjunct = MakeAdjunct("99/1/test-asset");
        A.CallTo(() => engineClient.AsynchronousIngest(adjunct, A<CancellationToken>._)).Returns(true);

        // Act
        await sut.SendIngestAdjunctRequest(adjunct);

        // Assert
        A.CallTo(() => customerQueueRepository.IncrementSize(99, QueueNames.Adjunct, 1, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SendIngestAdjunctRequest_Single_ReturnsTrue_WhenEngineSucceeds()
    {
        // Arrange
        var adjunct = MakeAdjunct("99/1/test-asset");
        A.CallTo(() => engineClient.AsynchronousIngest(adjunct, A<CancellationToken>._)).Returns(true);

        // Act
        var result = await sut.SendIngestAdjunctRequest(adjunct);

        // Assert
        result.Should().BeTrue();
        A.CallTo(() => customerQueueRepository.DecrementSize(A<int>._, A<string>._, A<int>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task SendIngestAdjunctRequest_Single_ReturnsFalse_WhenEngineFails()
    {
        // Arrange
        var adjunct = MakeAdjunct("99/1/test-asset");
        A.CallTo(() => engineClient.AsynchronousIngest(adjunct, A<CancellationToken>._)).Returns(false);

        // Act
        var result = await sut.SendIngestAdjunctRequest(adjunct);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendIngestAdjunctRequest_Single_DecrementsAdjunctQueue_WhenEngineFails()
    {
        // Arrange
        var adjunct = MakeAdjunct("99/1/test-asset");
        A.CallTo(() => engineClient.AsynchronousIngest(adjunct, A<CancellationToken>._)).Returns(false);

        // Act
        await sut.SendIngestAdjunctRequest(adjunct);

        // Assert
        A.CallTo(() => customerQueueRepository.DecrementSize(99, QueueNames.Adjunct, 1, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SendIngestAdjunctRequest_Batch_ReturnsZero_WhenAdjunctsEmpty()
    {
        // Act
        var result = await sut.SendIngestAdjunctRequest([]);

        // Assert
        result.Should().Be(0);
        A.CallTo(() => customerQueueRepository.IncrementSize(A<int>._, A<string>._, A<int>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => engineClient.AsynchronousIngestBatch(A<IReadOnlyCollection<Adjunct>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task SendIngestAdjunctRequest_Batch_IncrementsAdjunctQueueByCount()
    {
        // Arrange
        var adjuncts = new List<Adjunct> { MakeAdjunct("99/1/asset-1"), MakeAdjunct("99/1/asset-2") };
        A.CallTo(() => engineClient.AsynchronousIngestBatch(A<IReadOnlyCollection<Adjunct>>._, A<CancellationToken>._))
            .Returns(adjuncts.Count);

        // Act
        await sut.SendIngestAdjunctRequest(adjuncts);

        // Assert
        A.CallTo(() => customerQueueRepository.IncrementSize(99, QueueNames.Adjunct, 2, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SendIngestAdjunctRequest_Batch_ReturnsSentCount_WhenAllSucceed()
    {
        // Arrange
        var adjuncts = new List<Adjunct> { MakeAdjunct("99/1/asset-1"), MakeAdjunct("99/1/asset-2") };
        A.CallTo(() => engineClient.AsynchronousIngestBatch(A<IReadOnlyCollection<Adjunct>>._, A<CancellationToken>._))
            .Returns(2);

        // Act
        var result = await sut.SendIngestAdjunctRequest(adjuncts);

        // Assert
        result.Should().Be(2);
        A.CallTo(() => customerQueueRepository.DecrementSize(A<int>._, A<string>._, A<int>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task SendIngestAdjunctRequest_Batch_DecrementsAdjunctQueueByDifference_WhenSomeMessagesFail()
    {
        // Arrange
        var adjuncts = new List<Adjunct>
        {
            MakeAdjunct("99/1/asset-1"),
            MakeAdjunct("99/1/asset-2"),
            MakeAdjunct("99/1/asset-3")
        };
        A.CallTo(() => engineClient.AsynchronousIngestBatch(A<IReadOnlyCollection<Adjunct>>._, A<CancellationToken>._))
            .Returns(2); // 1 failed

        // Act
        var result = await sut.SendIngestAdjunctRequest(adjuncts);

        // Assert
        result.Should().Be(2);
        A.CallTo(() => customerQueueRepository.DecrementSize(99, QueueNames.Adjunct, 1, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    private static Adjunct MakeAdjunct(string assetId) => new()
    {
        Id = $"adjunct-for-{assetId}",
        MediaType = "text/plain",
        IIIFLink = IIIFLinkType.SeeAlso,
        AssetId = AssetId.FromString(assetId),
        Type = "test-type"
    };
}
