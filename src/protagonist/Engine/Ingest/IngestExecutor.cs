using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using Engine.Data;

namespace Engine.Ingest;

/// <summary>
/// Class to take asset, and execute workers in order, handling success/failure result and updating DB
/// </summary>
public class IngestExecutor
{
    private readonly WorkerBuilder workerBuilder;
    private readonly IEngineAssetRepository assetRepository;
    private readonly ILogger<IngestExecutor> logger;

    public IngestExecutor(WorkerBuilder workerBuilder, IEngineAssetRepository assetRepository,
        ILogger<IngestExecutor> logger)
    {
        this.workerBuilder = workerBuilder;
        this.assetRepository = assetRepository;
        this.logger = logger;
    }

    public async Task<IngestResult> IngestAsset(Asset asset, CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
        // TODO - should this class take serviceProvider and construct whole thing here?
        var workers = workerBuilder.GetWorkers(asset);
        
        var context = new IngestionContext(asset);

        var postProcessors = new List<IAssetIngesterPostProcess>(workers.Count);
        
        var overallStatus = IngestResultStatus.Unknown;
        foreach (var worker in workers)
        {
            if (worker is IAssetIngesterPostProcess process)
            {
                postProcessors.Add(process);
            }
            
            var result = await worker.Ingest(context, customerOriginStrategy, cancellationToken);
            if (result is IngestResultStatus.Failed or IngestResultStatus.StorageLimitExceeded)
            {
                overallStatus = result;
                break;
            }

            // TODO - make sure that a Success doesn't overwrite a Queued
            if (overallStatus != IngestResultStatus.QueuedForProcessing)
            {
                overallStatus = result;
            }
        }

        var dbSuccess = await CompleteAssetInDatabase(context, cancellationToken);

        if (!dbSuccess)
        {
            // TODO - Log warning or fail request? 
        }

        foreach (var postProcessor in postProcessors)
        {
            await postProcessor.PostIngest(context,
                dbSuccess && overallStatus is IngestResultStatus.Success or IngestResultStatus.QueuedForProcessing);
        }
        return new IngestResult(asset, dbSuccess ? overallStatus : IngestResultStatus.Failed);
    }

    private async Task<bool> CompleteAssetInDatabase(IngestionContext context, CancellationToken cancellationToken)
    {
        // TODO - will we have these for Timebased??
        if (string.IsNullOrWhiteSpace(context.Asset.MediaType) && context.AssetFromOrigin != null &&
            context.AssetFromOrigin.ContentType.HasText())
        {
            var contentType = context.AssetFromOrigin.ContentType;
            logger.LogInformation(
                "Setting mediaType for {AssetId} to {MediaType} as it was empty and received from origin",
                context.AssetId, contentType);
            context.Asset.MediaType = contentType;
        }

        var dbUpdateSuccess = await assetRepository.UpdateIngestedAsset(context.Asset, context.ImageLocation,
            context.ImageStorage, cancellationToken);
        return dbUpdateSuccess;
    }
}