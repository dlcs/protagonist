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
    private readonly IWorkerBuilder workerBuilder;
    private readonly IEngineAssetRepository assetRepository;
    private readonly ILogger<IngestExecutor> logger;

    public IngestExecutor(IWorkerBuilder workerBuilder, IEngineAssetRepository assetRepository,
        ILogger<IngestExecutor> logger)
    {
        this.workerBuilder = workerBuilder;
        this.assetRepository = assetRepository;
        this.logger = logger;
    }

    public async Task<IngestResult> IngestAsset(Asset asset, CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
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

            if (overallStatus != IngestResultStatus.QueuedForProcessing)
            {
                overallStatus = result;
            }
        }

        var dbSuccess = await CompleteAssetInDatabase(context, cancellationToken);
        
        foreach (var postProcessor in postProcessors)
        {
            await postProcessor.PostIngest(context,
                dbSuccess && overallStatus is IngestResultStatus.Success or IngestResultStatus.QueuedForProcessing);
        }
        return new IngestResult(asset, dbSuccess ? overallStatus : IngestResultStatus.Failed);
    }

    private async Task<bool> CompleteAssetInDatabase(IngestionContext context, CancellationToken cancellationToken)
    {
        var dbUpdateSuccess = await assetRepository.UpdateIngestedAsset(context.Asset, context.ImageLocation,
            context.ImageStorage, cancellationToken);
        return dbUpdateSuccess;
    }
}