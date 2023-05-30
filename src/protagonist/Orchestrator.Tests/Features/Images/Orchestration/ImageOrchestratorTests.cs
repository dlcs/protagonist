using System;
using System.IO;
using System.Threading;
using DLCS.Core.Caching;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Repository.Strategy;
using DLCS.Repository.Strategy.Utils;
using FakeItEasy;
using LazyCache;
using LazyCache.Mocks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Infrastructure.API;
using Orchestrator.Settings;
using Test.Helpers;
using Test.Helpers.Settings;

namespace Orchestrator.Tests.Features.Images.Orchestration;

public class ImageOrchestratorTests
{
    private readonly IOptionsMonitor<OrchestratorSettings> orchestratorSettings;
    private readonly IAssetTracker assetTracker;
    private readonly IOriginStrategy originStrategy;
    private readonly IAppCache fakedCache;
    private readonly IFileSaver fileSaver;
    private readonly IFileSystem fileSystem;
    private readonly IDlcsApiClient dlcsApiClient;

    private readonly OrchestrationImage orchestrationImage =
        new() { AssetId = new AssetId(1, 10, "test"), S3Location = "s3://here" };
    private const string CacheKey = "orch:1/10/test";

    public ImageOrchestratorTests()
    {
        assetTracker = A.Fake<IAssetTracker>();
        originStrategy = A.Fake<IOriginStrategy>();
        fakedCache = A.Fake<IAppCache>();
        fileSaver = A.Fake<IFileSaver>();
        fileSystem = A.Fake<IFileSystem>();
        dlcsApiClient = A.Fake<IDlcsApiClient>();

        var settings = new OrchestratorSettings
        {
            ImageFolderTemplateOrchestrator = "/wherever",
            Caching = new CacheSettings()
        };
        
        orchestratorSettings = OptionsHelpers.GetOptionsMonitor(settings);
    }

    [Fact]
    public async Task EnsureImageOrchestrated_DoesNotCheckFileSystem_IfObjectInCache()
    {
        // Arrange
        A.CallTo(() => fakedCache.GetOrAddAsync(CacheKey, A<Func<ICacheEntry, Task<bool>>>._)).Returns(true);
        var sut = GetSystemUnderTest(true);

        // Act
        var result = await sut.EnsureImageOrchestrated(orchestrationImage, CancellationToken.None);

        // Assert
        A.CallTo(() => fileSystem.FileExists(A<string>._)).MustNotHaveHappened();
        result.Should().Be(OrchestrationResult.AlreadyOrchestrated);
    }
    
    [Fact]
    public async Task EnsureImageOrchestrated_ChecksFileSystem_TouchesFile_AndReturnsIfExists()
    {
        // Arrange
        A.CallTo(() => fileSystem.FileExists(A<string>._)).Returns(true);
        var sut = GetSystemUnderTest();

        // Act
        var result = await sut.EnsureImageOrchestrated(orchestrationImage, CancellationToken.None);

        // Assert
        A.CallTo(() => fileSystem.SetLastWriteTimeUtc(A<string>._, A<DateTime>._)).MustHaveHappened();
        A.CallTo(() =>
                originStrategy.LoadAssetFromOrigin(orchestrationImage.AssetId, A<string>._, null,
                    CancellationToken.None))
            .MustNotHaveHappened();
        result.Should().Be(OrchestrationResult.AlreadyOrchestrated);
    }
    
    [Fact]
    public async Task EnsureImageOrchestrated_ChecksFileSystem_AndOrchesteratesIfNotExists()
    {
        // Arrange
        var originResponse = new OriginResponse("test".ToMemoryStream());
        A.CallTo(() =>
                originStrategy.LoadAssetFromOrigin(orchestrationImage.AssetId, A<string>._, null,
                    CancellationToken.None))
            .Returns(originResponse);
        var sut = GetSystemUnderTest();

        // Act
        var result = await sut.EnsureImageOrchestrated(orchestrationImage, CancellationToken.None);

        // Assert
        A.CallTo(() =>
                fileSaver.SaveResponseToDisk(orchestrationImage.AssetId, originResponse, A<string>._,
                    CancellationToken.None))
            .MustHaveHappened();
        result.Should().Be(OrchestrationResult.Orchestrated);
    }
    
    [Fact]
    public async Task EnsureImageOrchestrated_Throws_ReturnsError_IfOrchesetrationReturnsNullStream()
    {
        // Arrange
        A.CallTo(() =>
                originStrategy.LoadAssetFromOrigin(orchestrationImage.AssetId, A<string>._, null,
                    CancellationToken.None))
            .Returns(new OriginResponse(Stream.Null));
        var sut = GetSystemUnderTest();

        // Act
        var result = await sut.EnsureImageOrchestrated(orchestrationImage, CancellationToken.None);

        // Assert
        result.Should().Be(OrchestrationResult.Error);
    }
    
    [Fact]
    public async Task EnsureImageOrchestrated_ReingestsImage_IfReingestTrue()
    {
        // Arrange
        var image = new OrchestrationImage { AssetId = new AssetId(1, 10, "test"), Reingest = true };
        A.CallTo(() => dlcsApiClient.ReingestAsset(image.AssetId, CancellationToken.None)).Returns(true);
        var sut = GetSystemUnderTest();

        // Act
        await sut.EnsureImageOrchestrated(image, CancellationToken.None);

        // Assert
        A.CallTo(() => dlcsApiClient.ReingestAsset(image.AssetId, CancellationToken.None)).MustHaveHappened();
    }
    
    [Fact]
    public async Task EnsureImageOrchestrated_ReturnsError_IfReingestFails()
    {
        // Arrange
        var image = new OrchestrationImage { AssetId = new AssetId(1, 10, "test"), Reingest = true };
        A.CallTo(() => dlcsApiClient.ReingestAsset(image.AssetId, CancellationToken.None)).Returns(false);
        var sut = GetSystemUnderTest();

        // Act
        var result = await sut.EnsureImageOrchestrated(image, CancellationToken.None);

        // Assert
        result.Should().Be(OrchestrationResult.Error);
    }
    
    [Fact]
    public async Task EnsureImageOrchestrated_ReturnsError_IfReingestSucceedButRefreshFails()
    {
        // Arrange
        var image = new OrchestrationImage { AssetId = new AssetId(1, 10, "test"), Reingest = true };
        A.CallTo(() => dlcsApiClient.ReingestAsset(image.AssetId, CancellationToken.None)).Returns(true);
        A.CallTo(() => assetTracker.RefreshCachedAsset<OrchestrationImage>(image.AssetId))
            .Returns<OrchestrationImage>(null);
        var sut = GetSystemUnderTest();

        // Act
        var result = await sut.EnsureImageOrchestrated(image, CancellationToken.None);

        // Assert
        result.Should().Be(OrchestrationResult.Error);
    }
    
    [Fact]
    public async Task EnsureImageOrchestrated_ReturnsNotFound_IfNoS3Location()
    {
        // Arrange
        var image = new OrchestrationImage { AssetId = new AssetId(1, 10, "test") };
        A.CallTo(() => dlcsApiClient.ReingestAsset(image.AssetId, CancellationToken.None)).Returns(true);
        A.CallTo(() => assetTracker.RefreshCachedAsset<OrchestrationImage>(image.AssetId))
            .Returns<OrchestrationImage>(null);
        var sut = GetSystemUnderTest();

        // Act
        var result = await sut.EnsureImageOrchestrated(image, CancellationToken.None);

        // Assert
        result.Should().Be(OrchestrationResult.NotFound);
    }

    [Fact]
    public async Task EnsureImageOrchestrated_ReturnsNotFound_IfReingestSucceedButRefreshReturnsNoS3Location()
    {
        // Arrange
        var image = new OrchestrationImage { AssetId = new AssetId(1, 10, "test"), Reingest = true };
        A.CallTo(() => dlcsApiClient.ReingestAsset(image.AssetId, CancellationToken.None)).Returns(true);
        A.CallTo(() => assetTracker.RefreshCachedAsset<OrchestrationImage>(image.AssetId))
            .Returns(new OrchestrationImage { AssetId = new AssetId(1, 10, "test") });
        var sut = GetSystemUnderTest();

        // Act
        var result = await sut.EnsureImageOrchestrated(image, CancellationToken.None);

        // Assert
        result.Should().Be(OrchestrationResult.NotFound);
    }

    // if fakeCache = true then use A.Fake<IAppCache>, else use provided MockCachingService
    private ImageOrchestrator GetSystemUnderTest(bool fakeCache = false)
        => new(assetTracker, orchestratorSettings, originStrategy, fakeCache ? fakedCache : new MockCachingService(), 
            fileSaver, fileSystem, dlcsApiClient, new NullLogger<ImageOrchestrator>());
}
