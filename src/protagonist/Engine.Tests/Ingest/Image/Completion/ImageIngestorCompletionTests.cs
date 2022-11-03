using System.Net;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Engine.Data;
using Engine.Ingest;
using Engine.Ingest.Image.Completion;
using Engine.Ingest.Persistence;
using Engine.Settings;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Http;
using Test.Helpers.Settings;

namespace Engine.Tests.Ingest.Image.Completion;

public class ImageIngestorCompletionTests
{
    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly EngineSettings engineSettings;
    private readonly IEngineAssetRepository assetRepository;
    private readonly IFileSystem fileSystem;
    private readonly ImageIngestorCompletion sut;

    public ImageIngestorCompletionTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        engineSettings = new EngineSettings { ImageIngest = new ImageIngestSettings() };
        assetRepository = A.Fake<IEngineAssetRepository>();
        fileSystem = A.Fake<IFileSystem>();

        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(engineSettings);

        var httpClient = new HttpClient(httpHandler);
        httpClient.BaseAddress = new Uri("http://orchestrator/");
        var orchestratorClient = new OrchestratorClient(httpClient, new NullLogger<OrchestratorClient>());
        sut = new ImageIngestorCompletion(assetRepository, optionsMonitor, orchestratorClient, fileSystem,
            new NullLogger<ImageIngestorCompletion>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompleteIngestion_CallsRepositoryUpdateIngestedAsset_RegardlessOfSuccess(bool success)
    {
        // Arrange
        var assetId = AssetId.FromString("2/1/foo-bar");
        var asset = new Asset(assetId);
        var imageLocation = new ImageLocation { Id = asset.Id };
        var imageStorage = new ImageStorage { Id = asset.Id };
        var context = new IngestionContext(asset).WithAssetFromOrigin(new AssetFromOrigin(assetId, 0, null, null));
        context.WithLocation(imageLocation).WithStorage(imageStorage);

        // Act
        await sut.CompleteIngestion(context, success, "");

        // Assert
        A.CallTo(() => assetRepository.UpdateIngestedAsset(asset, imageLocation, imageStorage, A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Theory]
    [InlineData(false, false, false, false)] // don't orchestrate, no success
    [InlineData(false, false, true, false)] // don't orchestrate, ingest success, mark fail
    [InlineData(false, false, false, true)] // don't orchestrate, ingest fail, mark success
    [InlineData(false, false, true, true)] // don't orchestrate, ingest + mark success
    [InlineData(true, false, false, false)] // don't orchestrate due to override, no success
    [InlineData(true, false, true, false)] // don't orchestrate due to override, ingest success, mark fail
    [InlineData(true, false, false, true)] // don't orchestrate due to override, ingest fail, mark success
    [InlineData(true, false, true, true)] // don't orchestrate due to override, ingest + mark success
    [InlineData(false, true, false, false)] // orchestrate due to override, no success
    [InlineData(false, true, true, false)] // orchestrate due to override, ingest success, mark fail
    [InlineData(false, true, false, true)] // orchestrate due to override, ingest fail, mark success
    [InlineData(true, true, false, false)] // orchestrate due to both, no success
    [InlineData(true, true, true, false)] // orchestrate due to both, ingest success, mark fail
    [InlineData(true, true, false, true)] // orchestrate due to both, ingest fail, mark success
    public async Task CompleteIngestion_DoesNotOrchestrate_IfAnyFail_OrOrchestrateAfterIngestFalse(
        bool theDefault, bool theOverride, bool ingestSuccess, bool markAsCompleteSuccess)
    {
        // Arrange
        var assetId = AssetId.FromString("2/1/foo-bar");
        var asset = new Asset(assetId);
        var imageLocation = new ImageLocation { Id = asset.Id };
        var imageStorage = new ImageStorage { Id = asset.Id };
        var context = new IngestionContext(asset)
            .WithAssetFromOrigin(new AssetFromOrigin(assetId, 0, null, null))
            .WithLocation(imageLocation)
            .WithStorage(imageStorage);
        
        engineSettings.ImageIngest.OrchestrateImageAfterIngest = theDefault;
        engineSettings.CustomerOverrides.Add(asset.Customer.ToString(),
            new CustomerOverridesSettings { OrchestrateImageAfterIngest = theOverride });
        A.CallTo(() => assetRepository.UpdateIngestedAsset(asset, imageLocation, imageStorage, A<CancellationToken>._))
            .Returns(markAsCompleteSuccess);

        // Act
        await sut.CompleteIngestion(context, ingestSuccess, "");

        // Assert
        httpHandler.CallsMade.Should().BeNullOrEmpty();
    }

    [Theory]
    [InlineData(false, true)] // orchestrate due to override
    [InlineData(true, true)] // orchestrate due to both
    public async Task CompleteIngestion_Orchestrates_IfOperationsSuccessful_AndOverrideOrDefaultTrue(
        bool theDefault, bool theOverride)
    {
        // Arrange
        var assetId = AssetId.FromString("2/1/foo-bar");
        var asset = new Asset(assetId);
        var imageLocation = new ImageLocation { Id = asset.Id };
        var imageStorage = new ImageStorage { Id = asset.Id };
        var context = new IngestionContext(asset)
            .WithAssetFromOrigin(new AssetFromOrigin(assetId, 0, null, null))
            .WithLocation(imageLocation)
            .WithStorage(imageStorage);
        engineSettings.ImageIngest.OrchestrateImageAfterIngest = theDefault;
        engineSettings.CustomerOverrides.Add(asset.Customer.ToString(),
            new CustomerOverridesSettings { OrchestrateImageAfterIngest = theOverride });
        A.CallTo(() => assetRepository.UpdateIngestedAsset(asset, imageLocation, imageStorage, A<CancellationToken>._))
            .Returns(true);
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await sut.CompleteIngestion(context, true, "");

        // Assert
        httpHandler.CallsMade.Should().Contain("http://orchestrator/iiif-img/2/1/foo-bar/info.json");
    }
}