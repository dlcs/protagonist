using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Model.Customers;
using Engine.Ingest.Persistence;

namespace Engine.Ingest.File;

/// <summary>
/// <see cref="IAssetIngester"/> implementation for handling "file" delivery-channel
/// </summary>
public class FileChannelWorker : IAssetIngesterWorker
{
    private readonly IAssetToS3 assetToS3;
    private readonly IAssetIngestorSizeCheck assetIngestorSizeCheck;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly ILogger<FileChannelWorker> logger;

    public FileChannelWorker(
        IAssetToS3 assetToS3,
        IAssetIngestorSizeCheck assetIngestorSizeCheck,
        IStorageKeyGenerator storageKeyGenerator,
        ILogger<FileChannelWorker> logger)
    {
        this.assetToS3 = assetToS3;
        this.assetIngestorSizeCheck = assetIngestorSizeCheck;
        this.storageKeyGenerator = storageKeyGenerator;
        this.logger = logger;
    }
    
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
                asset,
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

    private static void UpdateIngestionContext(IngestionContext ingestionContext, AssetFromOrigin assetInBucket,
        RegionalisedObjectInBucket targetStorageLocation)
    {
        ingestionContext.StoredObjects[targetStorageLocation] = assetInBucket.AssetSize;
        ingestionContext.WithStorage(assetSize: assetInBucket.AssetSize);
    }
}