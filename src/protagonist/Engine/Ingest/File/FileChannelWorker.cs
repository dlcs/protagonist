using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Model.Customers;
using Engine.Ingest.Persistence;

namespace Engine.Ingest.File;

/// <summary>
/// <see cref="IAssetIngester"/> implementation for handling "file" delivery-channel
/// </summary>
public class FileChannelWorker(
    IAssetToS3 assetToS3,
    IAdjunctToS3 adjunctToS3,
    IAssetIngestorSizeCheck assetIngestorSizeCheck,
    IStorageKeyGenerator storageKeyGenerator,
    ILogger<FileChannelWorker> logger)
    : IAssetIngesterWorker, IAdjunctIngesterWorker
{
    public async Task<IngestResultStatus> Ingest(IngestionContext ingestionContext,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        var asset = ingestionContext.Asset;

        try
        {
            if (customerOriginStrategy.Optimised)
            {
                logger.LogDebug("Asset {Asset} is at optimised origin, no 'file' handling required",
                    ingestionContext.AssetId);
                return IngestResultStatus.Success;
            }

            var targetStorageLocation = storageKeyGenerator.GetStoredOriginalLocation(ingestionContext.AssetId);

            var assetInBucket = await assetToS3.CopyOriginToStorage(targetStorageLocation,
                ingestionContext,
                !assetIngestorSizeCheck.CustomerHasNoStorageCheck(asset.Customer),
                customerOriginStrategy,
                cancellationToken);

            ingestionContext.WithAssetFromOrigin(assetInBucket);

            if (assetIngestorSizeCheck.DoesAssetFromOriginExceedAllowance(assetInBucket, asset))
            {
                return IngestResultStatus.StorageLimitExceeded;
            }

            UpdateIngestionContext(ingestionContext, assetInBucket, targetStorageLocation);
            return IngestResultStatus.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting asset {AssetId} for file channel", asset.Id);
            asset.Error = ex.Message;
            return IngestResultStatus.Failed;
        }
    }
    
    private static void UpdateIngestionContext(IngestionContext ingestionContext, AssetFromOrigin itemInBucket,
        RegionalisedObjectInBucket targetStorageLocation)
    {
        ingestionContext.StoredObjects[targetStorageLocation] = itemInBucket.AssetSize;
        ingestionContext.WithStorage(assetSize: itemInBucket.AssetSize);
    }
    
    private static void UpdateIngestionContext(AdjunctIngestionContext ingestionContext, AdjunctFromOrigin itemInBucket,
        RegionalisedObjectInBucket targetStorageLocation)
    {
        ingestionContext.StoredObjects[targetStorageLocation] = itemInBucket.AssetSize;
        ingestionContext.WithStorage(adjunctSize: itemInBucket.AssetSize);
    }

    public async Task<IngestResultStatus> Ingest(AdjunctIngestionContext ingestionContext,
        CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken = default)
    {
        var adjunct = ingestionContext.Adjunct;

        try
        {
            if (customerOriginStrategy.Optimised)
            {
                logger.LogDebug(
                    "Adjunct {AdjunctId} for Asset {Asset} is at optimised origin, no 'file' handling required",
                    adjunct.Id, ingestionContext.AssetId);
                return IngestResultStatus.Success;
            }

            var targetStorageLocation = storageKeyGenerator.GetStoredAdjunctLocation(ingestionContext.AssetId, adjunct);

            var adjunctInBucket = await adjunctToS3.CopyAdjunctToStorage(targetStorageLocation,
                ingestionContext,
                !assetIngestorSizeCheck.CustomerHasNoStorageCheck(ingestionContext.Asset.Customer),
                customerOriginStrategy,
                cancellationToken);
            
            if (assetIngestorSizeCheck.DoesAssetFromOriginExceedAllowance(adjunctInBucket, adjunct))
            {
                return IngestResultStatus.StorageLimitExceeded;
            }

            UpdateIngestionContext(ingestionContext, adjunctInBucket, targetStorageLocation);
            return IngestResultStatus.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting asset Adjunct {AdjunctId} for Asset {Asset} for file channel",
                adjunct.Id, ingestionContext.AssetId);
            adjunct.Error = ex.Message;
            return IngestResultStatus.Failed;
        }
    }
}
