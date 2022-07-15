using DLCS.Core.FileSystem;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Completion;

public class ImageIngestorCompletion : IImageIngestorCompletion
{
    private readonly IEngineAssetRepository assetRepository;
    private readonly OrchestratorClient orchestratorClient;
    private readonly IFileSystem fileSystem;
    private readonly ILogger<ImageIngestorCompletion> logger;
    private readonly EngineSettings engineSettings;

    public ImageIngestorCompletion(
        IEngineAssetRepository assetRepository,
        IOptionsMonitor<EngineSettings> engineOptions,
        OrchestratorClient orchestratorClient,
        IFileSystem fileSystem,
        ILogger<ImageIngestorCompletion> logger)
    {
        this.assetRepository = assetRepository;
        this.orchestratorClient = orchestratorClient;
        this.fileSystem = fileSystem;
        this.logger = logger;
        engineSettings = engineOptions.CurrentValue;
    }
    
    /// <summary>
    /// Mark asset as completed in database, clean up working assets and optionally trigger orchestration.
    /// </summary>
    public async Task<bool> CompleteIngestion(IngestionContext context, bool ingestSuccessful, string? sourceTemplate)
    {
        var dbUpdateSuccess =
            await assetRepository.UpdateIngestedAsset(context.Asset, context.ImageLocation, context.ImageStorage);
        
        if (ingestSuccessful && dbUpdateSuccess)
        {
            await TriggerOrchestration(context);
        }
        
        // Processing has occurred, clear down the root folder used for processing
        CleanupWorkingAssets(sourceTemplate);

        return dbUpdateSuccess;
    }

    private async Task TriggerOrchestration(IngestionContext context)
    {
        if (!ShouldOrchestrate(context.Asset.Customer)) return;

        await orchestratorClient.TriggerOrchestration(context.AssetId);
    }
    
    private bool ShouldOrchestrate(int customerId)
    {
        var customerSpecific = engineSettings.GetCustomerSettings(customerId);
        return customerSpecific.OrchestrateImageAfterIngest ?? engineSettings.OrchestrateImageAfterIngest;
    }
    
    private void CleanupWorkingAssets(string? rootPath)
    {
        if (string.IsNullOrEmpty(rootPath)) return;
        
        try
        {
            fileSystem.DeleteDirectory(rootPath, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error cleaning up working assets from '{RootPath}'", rootPath);
        }
    }
}
