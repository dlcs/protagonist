using DLCS.AWS.S3;
using DLCS.Core.FileSystem;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Image.Completion;

public interface IImageIngestPostProcessing
{
    Task CompleteIngestion(IngestionContext ingestionContext, bool ingestSuccessful);
}

/// <summary>
/// Class that contains logic for post-processing of image assets.
/// </summary>
public class ImageIngestPostProcessing : IImageIngestPostProcessing
{
    private readonly IOrchestratorClient orchestratorClient;
    private readonly ILogger<ImageIngestPostProcessing> logger;
    private readonly IBucketWriter bucketWriter;
    private readonly IFileSystem fileSystem;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly EngineSettings engineSettings;

    public ImageIngestPostProcessing(
        IOrchestratorClient orchestratorClient,
        IOptionsMonitor<EngineSettings> engineOptions,
        IBucketWriter bucketWriter,
        IFileSystem fileSystem,
        IStorageKeyGenerator storageKeyGenerator,
        ILogger<ImageIngestPostProcessing> logger)
    {
        this.orchestratorClient = orchestratorClient;
        this.logger = logger;
        this.bucketWriter = bucketWriter;
        this.fileSystem = fileSystem;
        this.storageKeyGenerator = storageKeyGenerator;
        engineSettings = engineOptions.CurrentValue;
    }
    
    public async Task CompleteIngestion(IngestionContext ingestionContext, bool ingestSuccessful)
    {
        try
        {
            // Optionally trigger info.json request to orchestrate 
            var orchestrate = OrchestrateIfRequired(ingestionContext, ingestSuccessful);
            
            // Delete info.json
            var deleteInfoJson = DeleteInfoJson(ingestionContext, ingestSuccessful);

            await Task.WhenAll(orchestrate, deleteInfoJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error completing {AssetId}", ingestionContext.AssetId);
        }
        finally
        {
            // Delete local folder used for image ingestion + processing
            DeleteWorkingFolder(ingestionContext);
        }
    }

    private async Task DeleteInfoJson(IngestionContext ingestionContext, bool ingestSuccessful)
    {
        if (!ingestSuccessful) return;
        
        var infoJsonRoot = storageKeyGenerator.GetInfoJsonRoot(ingestionContext.AssetId);
        await bucketWriter.DeleteFolder(infoJsonRoot, false);
    }

    private async Task OrchestrateIfRequired(IngestionContext ingestionContext, bool ingestSuccessful)
    {
        if (!ingestSuccessful) return;
        if (!ShouldOrchestrate(ingestionContext.Asset.Customer)) return;
        
        logger.LogDebug("Triggering ingestion after orchestration for {AssetId}", ingestionContext.AssetId);
        await orchestratorClient.TriggerOrchestration(ingestionContext.AssetId);
    } 

    private bool ShouldOrchestrate(int customerId)
    {
        var customerSpecific = engineSettings.GetCustomerSettings(customerId);
        return customerSpecific.OrchestrateImageAfterIngest ?? engineSettings.ImageIngest!.OrchestrateImageAfterIngest;
    }

    private void DeleteWorkingFolder(IngestionContext ingestionContext)
    {
        var sourceTemplate = ImageIngestionHelpers.GetWorkingFolder(ingestionContext.IngestId, engineSettings.ImageIngest!);
        fileSystem.DeleteDirectory(sourceTemplate, true);
    }
}
