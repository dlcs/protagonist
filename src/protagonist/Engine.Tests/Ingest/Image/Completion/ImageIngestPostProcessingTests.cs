using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Engine.Ingest;
using Engine.Ingest.Image.Completion;
using Engine.Settings;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers;
using Test.Helpers.Settings;

namespace Engine.Tests.Ingest.Image.Completion;

public class ImageIngestPostProcessingTests
{
    private readonly IOrchestratorClient orchestratorClient;
    private readonly IBucketWriter bucketWriter;
    private readonly FakeFileSystem fileSystem;
    private readonly IStorageKeyGenerator storageKeyGenerator;

    public ImageIngestPostProcessingTests()
    {
        orchestratorClient = A.Fake<IOrchestratorClient>();
        bucketWriter = A.Fake<IBucketWriter>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        fileSystem = new FakeFileSystem();

        A.CallTo(() => storageKeyGenerator.GetInfoJsonRoot(A<AssetId>._))
            .ReturnsLazily((AssetId assetId) => new ObjectInBucket("the-bucket", $"{assetId}/info/"));
    }

    private ImageIngestPostProcessing GetSut(bool orchestrateAfterIngest)
    {
        var engineSettings = new EngineSettings
        {
            ImageIngest = new ImageIngestSettings
            {
                SourceTemplate = "{customer}_{space}_{image}",
                OrchestrateImageAfterIngest = orchestrateAfterIngest,
                ScratchRoot = "scratch/"
            },
        };
        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(engineSettings);

        return new ImageIngestPostProcessing(orchestratorClient, optionsMonitor, bucketWriter, fileSystem,
            storageKeyGenerator, new NullLogger<ImageIngestPostProcessing>());
    }
    
    [Fact]
    public async Task PostIngest_CallsOrchestrator_IfSuccessfulIngest()
    {
        // Arrange
        var assetId = AssetId.FromString("2/1/avalon");
        var asset = new Asset(assetId);
        var sut = GetSut(true);

        // Act
        await sut.CompleteIngestion(new IngestionContext(asset), true);
        
        // Assert
        A.CallTo(() => orchestratorClient.TriggerOrchestration(assetId)).MustHaveHappened();
    }
    
    [Fact]
    public async Task PostIngest_DoesNotCallOrchestrator_IfSuccessfulIngest_ButOrchestrateAfterIngestFalse()
    {
        // Arrange
        var assetId = AssetId.FromString("2/1/avalon");
        var asset = new Asset(assetId);
        var sut = GetSut(false);

        // Act
        await sut.CompleteIngestion(new IngestionContext(asset), true);
        
        // Assert
        A.CallTo(() => orchestratorClient.TriggerOrchestration(assetId)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task PostIngest_DoesNotCallOrchestrator_IfIngestFailed()
    {
        // Arrange
        var assetId = AssetId.FromString("2/1/avalon");
        var asset = new Asset(assetId);
        var sut = GetSut(true);

        // Act
        await sut.CompleteIngestion(new IngestionContext(asset), false);
        
        // Assert
        A.CallTo(() => orchestratorClient.TriggerOrchestration(assetId)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task PostIngest_DeletesInfoJson_IfSuccessfulIngest()
    {
        // Arrange
        var assetId = AssetId.FromString("2/1/avalon");
        var asset = new Asset(assetId);
        var sut = GetSut(true);

        // Act
        await sut.CompleteIngestion(new IngestionContext(asset), true);
        
        // Assert
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>.That.Matches(o => o.Key == $"{assetId}/info/"), A<bool>._))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task PostIngest_DoesNotDeleteInfoJson_IfIngestFailed()
    {
        // Arrange
        var assetId = AssetId.FromString("2/1/avalon");
        var asset = new Asset(assetId);
        var sut = GetSut(true);

        // Act
        await sut.CompleteIngestion(new IngestionContext(asset), false);
        
        // Assert
        A.CallTo(() => bucketWriter.DeleteFolder(A<ObjectInBucket>._, A<bool>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PostIngest_DeletesWorkingFolder_IfSuccessOrFailure(bool success)
    {
        // Arrange
        var assetId = AssetId.FromString("2/1/avalon");
        var asset = new Asset(assetId);
        var sut = GetSut(true);
        var context = new IngestionContext(asset);
        
        // Act
        await sut.CompleteIngestion(context, success);
        
        // Assert
        var expectedWorkingFolder = $"scratch/{context.IngestId}/";
        fileSystem.DeletedDirectories.Should().Contain(d => 
            // Account for backslashes being used to separate directories when running on Windows
            d.Replace("\\", "/") == expectedWorkingFolder);
    }
}