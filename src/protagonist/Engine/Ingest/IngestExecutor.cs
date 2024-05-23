using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Storage;
using Engine.Data;
using Engine.Ingest.Models;

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
    private readonly IStorageRepository storageRepository;
    private const int MinimumAssetSize = 100;

    public IngestExecutor(IWorkerBuilder workerBuilder, 
        IEngineAssetRepository assetRepository,
        IAssetIngestorSizeCheck assetIngestorSizeCheck,
        IStorageRepository storageRepository,
        ILogger<IngestExecutor> logger)
    {
        this.workerBuilder = workerBuilder;
        this.assetRepository = assetRepository;
        this.logger = logger;
        this.assetIngestorSizeCheck = assetIngestorSizeCheck;
        this.storageRepository = storageRepository;
    }

    public async Task<IngestResult> IngestAsset(Asset asset, CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
        var context = new IngestionContext(asset);

        // If the asset has the `none` delivery channel specified, skip processing and mark the ingest as being complete
        if (asset.HasSingleDeliveryChannel(AssetDeliveryChannels.None))
        {
            var imageStorage = new ImageStorage
            {
                Id = asset.Id,
                Customer = asset.Customer,
                Space = asset.Space,
                Size = 0,
                LastChecked = DateTime.UtcNow,
                ThumbnailSize = 0,
            };
            await assetRepository.UpdateIngestedAsset(context.Asset, null, imageStorage, 
                true, cancellationToken);
            return new IngestResult(asset.Id, IngestResultStatus.Success);
        }
        
        var workers = workerBuilder.GetWorkers(asset);
        var overallStatus = IngestResultStatus.Unknown;

        if (!assetIngestorSizeCheck.CustomerHasNoStorageCheck(asset.Customer))
        {
            var counts = await storageRepository.GetStorageMetrics(asset.Customer, cancellationToken);
            
            if (!counts.CanStoreAssetSize(MinimumAssetSize, 0))
            {
                logger.LogDebug("Storage policy exceeded for customer {CustomerId} with id {Id}", asset.Customer, asset.Id);
                asset.Error = IngestErrors.StoragePolicyExceeded;
                var dbResponse = await CompleteAssetInDatabase(context, true, cancellationToken);
                return new IngestResult(asset.Id, dbResponse ? IngestResultStatus.StorageLimitExceeded : IngestResultStatus.Failed);
            }
            
            var preIngestionAssetSize = await assetRepository.GetImageSize(asset.Id, cancellationToken);
            context.WithPreIngestionAssetSize(preIngestionAssetSize);
        }
        
        var postProcessors = new List<IAssetIngesterPostProcess>(workers.Count);

        if (overallStatus != IngestResultStatus.StorageLimitExceeded)
        {
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
        }

        var dbSuccess = await CompleteAssetInDatabase(context, overallStatus != IngestResultStatus.QueuedForProcessing,
            cancellationToken);
        
        foreach (var postProcessor in postProcessors)
        {
            logger.LogDebug("Calling {Worker} post-process for {AssetId}", postProcessor.GetType(), asset.Id);
            await postProcessor.PostIngest(context,
                dbSuccess && overallStatus is IngestResultStatus.Success or IngestResultStatus.QueuedForProcessing);
        }
        return new IngestResult(asset.Id, dbSuccess ? overallStatus : IngestResultStatus.Failed);
    }

    private async Task<bool> CompleteAssetInDatabase(IngestionContext context, bool ingestFinished, CancellationToken cancellationToken)
    {
        var dbUpdateSuccess = await assetRepository.UpdateIngestedAsset(context.Asset, context.ImageLocation,
            context.ImageStorage, ingestFinished, cancellationToken);
        return dbUpdateSuccess;
    }
}