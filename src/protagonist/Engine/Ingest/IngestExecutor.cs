using System.Diagnostics;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Storage;
using Engine.Data;
using Engine.Ingest.Models;

namespace Engine.Ingest;

/// <summary>
/// Class to take asset, and execute workers in order, handling success/failure result and updating DB
/// </summary>
public class IngestExecutor(
    IWorkerBuilder workerBuilder,
    IEngineAssetRepository assetRepository,
    IAssetIngestorSizeCheck assetIngestorSizeCheck,
    IStorageRepository storageRepository,
    ILogger<IngestExecutor> logger)
{
    private const int MinimumAssetSize = 100;

    public async Task<AdjunctIngestResult> IngestAdjunct(Adjunct adjunct, CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var context = new AdjunctIngestionContext(adjunct);

        var customerId = adjunct.Asset.Customer;
        var assetId = adjunct.Asset.Id;

        if (!assetIngestorSizeCheck.CustomerHasNoStorageCheck(customerId))
        {
            var counts = await storageRepository.GetStorageMetrics(customerId, cancellationToken);

            if (!counts.CanStoreAssetSize(MinimumAssetSize, 0))
            {
                logger.LogDebug(
                    "Storage policy exceeded for customer {CustomerId} with asset id {AssetId}, adjunct id {AdjunctId}",
                    customerId, assetId, adjunct.Id);

                adjunct.Error = IngestErrors.StoragePolicyExceeded;
                var dbResponse = await CompleteAssetInDatabase(context, true, cancellationToken);
                return new AdjunctIngestResult(adjunct.Id, adjunct.AssetId,
                    dbResponse ? IngestResultStatus.StorageLimitExceeded : IngestResultStatus.Failed);
            }

            var preIngestionAssetSize = adjunct.Size;
            context.WithPreIngestionAssetSize(preIngestionAssetSize);
        }

        var workers = workerBuilder.GetWorkers(adjunct);
        var overallStatus = IngestResultStatus.Unknown;
        var postProcessors = new List<IAdjunctIngesterPostProcess>(workers.Count);

        foreach (var worker in workers)
        {
            // NOTE: currently there is no implementer of this interface, and it's marked as potential issue,
            // hence the disable below. The analogous flow to Assets was implemented for Adjuncts so that when
            // any Adjunct post-processing is to be added, it can avoid having to find and modify this bit.
            // The 'disable' can be removed once that happens.
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (worker is IAdjunctIngesterPostProcess process)
            {
                postProcessors.Add(process);
            }

            logger.LogDebug("Calling {Worker} for adjunct {AdjunctId}, asset {AssetId}", worker.GetType(), adjunct.Id,
                adjunct.AssetId);
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

        var dbSuccess = await CompleteAdjunctInDatabase(context,
            overallStatus != IngestResultStatus.QueuedForProcessing,
            cancellationToken);

        foreach (var postProcessor in postProcessors)
        {
            logger.LogDebug("Calling {Worker} post-process for adjunct {AdjunctId}, asset {AssetId}",
                postProcessor.GetType(), adjunct.Id, adjunct.AssetId);
            await postProcessor.PostIngest(context,
                dbSuccess && overallStatus is IngestResultStatus.Success or IngestResultStatus.QueuedForProcessing);
        }

        sw.Stop();
        logger.LogDebug("Processed {AdjunctId}, asset {AssetId} in {Elapsed}ms", adjunct.Id, adjunct.AssetId,
            sw.ElapsedMilliseconds);
        return new AdjunctIngestResult(adjunct.Id, adjunct.AssetId,
            dbSuccess ? overallStatus : IngestResultStatus.Failed);
    }

    public async Task<IngestResult> IngestAsset(Asset asset, CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var context = new IngestionContext(asset);

        // If the asset has the `none` delivery channel specified, skip processing and mark the ingest as being complete
        if (asset.HasSingleDeliveryChannel(AssetDeliveryChannels.None))
        {
            context.WithStorage();
            await assetRepository.UpdateIngestedAsset(context.Asset, null, context.ImageStorage, 
                true, cancellationToken);
            return new IngestResult(asset.Id, IngestResultStatus.Success);
        }
        
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
        
        var workers = workerBuilder.GetWorkers(asset);
        var overallStatus = IngestResultStatus.Unknown;
        var postProcessors = new List<IAssetIngesterPostProcess>(workers.Count);

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
        
        sw.Stop();
        logger.LogDebug("Processed {AssetId} in {Elapsed}ms", asset.Id, sw.ElapsedMilliseconds);
        return new IngestResult(asset.Id, dbSuccess ? overallStatus : IngestResultStatus.Failed);
    }

    private async Task<bool> CompleteAdjunctInDatabase(AdjunctIngestionContext context, bool ingestFinished,
        CancellationToken cancellationToken)
    {
        var dbUpdateSuccess = await assetRepository.UpdateIngestedAdjunct(context.Adjunct, context.ImageStorage,
            ingestFinished, cancellationToken);
        return dbUpdateSuccess;
    }
    
    private async Task<bool> CompleteAssetInDatabase(IngestionContext context, bool ingestFinished, CancellationToken cancellationToken)
    {
        var dbUpdateSuccess = await assetRepository.UpdateIngestedAsset(context.Asset, context.ImageLocation,
            context.ImageStorage, ingestFinished, cancellationToken);
        return dbUpdateSuccess;
    }
}
