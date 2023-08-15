using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Storage;
using Engine.Data;

namespace Engine.Ingest;

/// <summary>
/// Class to take asset, and execute workers in order, handling success/failure result and updating DB
/// </summary>
public class IngestExecutor
{
    private readonly IWorkerBuilder workerBuilder;
    private readonly IEngineAssetRepository assetRepository;
    private readonly ILogger<IngestExecutor> logger;
    private readonly IAssetIngestorSizeCheck assetIngestorSizeCheck;

    public IngestExecutor(IWorkerBuilder workerBuilder, 
        IEngineAssetRepository assetRepository,
        IAssetIngestorSizeCheck assetIngestorSizeCheck,
        ILogger<IngestExecutor> logger)
    {
        this.workerBuilder = workerBuilder;
        this.assetRepository = assetRepository;
        this.logger = logger;
        this.assetIngestorSizeCheck = assetIngestorSizeCheck;
    }

    public async Task<IngestResult> IngestAsset(Asset asset, CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
        var workers = workerBuilder.GetWorkers(asset);
        
        var context = new IngestionContext(asset);
        long? preIngestionAssetSize = null;

        if (!assetIngestorSizeCheck.CustomerHasNoStorageCheck(asset.Customer))
        {
            preIngestionAssetSize = await assetRepository.GetImageSize(asset.Id, cancellationToken);
        }

        context.WithPreIngestionAssetSize(preIngestionAssetSize);

        var postProcessors = new List<IAssetIngesterPostProcess>(workers.Count);
        
        var overallStatus = IngestResultStatus.Unknown;
        foreach (var worker in workers)
        {
            if (worker is IAssetIngesterPostProcess process)
            {
                postProcessors.Add(process);
            }

            logger.LogDebug("Calling {Worker} for {AssetId}..", worker.GetType(), asset.Id);
            var result = await worker.Ingest(context, customerOriginStrategy, cancellationToken);
            if (result is IngestResultStatus.Failed or IngestResultStatus.StorageLimitExceeded)
            {
                overallStatus = result;
                break;
            }

            // Don't overwrite a QueuedForProcessing result - this wins
            if (overallStatus != IngestResultStatus.QueuedForProcessing)
            {
                overallStatus = result;
            }
        }

        var dbSuccess = await CompleteAssetInDatabase(context, overallStatus != IngestResultStatus.QueuedForProcessing,
            cancellationToken);
        
        foreach (var postProcessor in postProcessors)
        {
            logger.LogDebug("Calling {Worker} post-process for {AssetId}", postProcessor.GetType(), asset.Id);
            await postProcessor.PostIngest(context,
                dbSuccess && overallStatus is IngestResultStatus.Success or IngestResultStatus.QueuedForProcessing);
        }
        return new IngestResult(asset, dbSuccess ? overallStatus : IngestResultStatus.Failed);
    }

    private async Task<bool> CompleteAssetInDatabase(IngestionContext context, bool ingestFinished, CancellationToken cancellationToken)
    {
        var dbUpdateSuccess = await assetRepository.UpdateIngestedAsset(context.Asset, context.ImageLocation,
            context.ImageStorage, ingestFinished, cancellationToken);
        return dbUpdateSuccess;
    }
}