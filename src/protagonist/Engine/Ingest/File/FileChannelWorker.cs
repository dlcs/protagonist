using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using Engine.Ingest.Persistence;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.File;

/// <summary>
/// <see cref="IAssetIngester"/> implementation for handling "file" delivery-channel
/// </summary>
public class FileChannelWorker : IAssetIngesterWorker
{
    private readonly IAssetToS3 assetToS3;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly ILogger<FileChannelWorker> logger;
    private readonly EngineSettings engineSettings;

    public FileChannelWorker(
        IAssetToS3 assetToS3,
        IOptionsMonitor<EngineSettings> engineOptions,
        IStorageKeyGenerator storageKeyGenerator,
        ILogger<FileChannelWorker> logger)
    {
        this.assetToS3 = assetToS3;
        engineSettings = engineOptions.CurrentValue;
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

            if (HasFileAlreadyBeenCopied(ingestionContext, out var targetStorageLocation))
            {
                logger.LogDebug("Asset {Asset} has already been uploaded to {S3Location}, no 'file' handling required",
                    ingestionContext.AssetId, targetStorageLocation);
                return IngestResultStatus.Success;
            }

            var assetInBucket = await assetToS3.CopyOriginToStorage(targetStorageLocation,
                asset,
                !SkipStoragePolicyCheck(asset.Customer),
                customerOriginStrategy,
                cancellationToken);
            ingestionContext.WithAssetFromOrigin(assetInBucket);
            
            if (assetInBucket.FileExceedsAllowance)
            {
                asset.Error = "StoragePolicy size limit exceeded";
                return IngestResultStatus.StorageLimitExceeded;
            }

            UpdateImageStorage(ingestionContext, asset, assetInBucket);
            return IngestResultStatus.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ingesting asset {AssetId} for file channel", asset.Id);
            asset.Error = ex.Message;
            return IngestResultStatus.Failed;
        }
    }

    private static void UpdateImageStorage(IngestionContext ingestionContext, Asset asset, AssetFromOrigin assetInBucket)
    {
        if (ingestionContext.ImageStorage == null)
        {
            var imageStorage = new ImageStorage
            {
                Id = asset.Id,
                Customer = asset.Customer,
                Space = asset.Space,
            };

            ingestionContext.WithStorage(imageStorage);
        }

        ingestionContext.ImageStorage!.Size += assetInBucket.AssetSize;
        ingestionContext.ImageStorage.LastChecked = DateTime.UtcNow;
    }

    private bool HasFileAlreadyBeenCopied(IngestionContext ingestionContext, out RegionalisedObjectInBucket targetStorageLocation)
    {
        targetStorageLocation = storageKeyGenerator.GetStoredOriginalLocation(ingestionContext.AssetId);
        var exists = ingestionContext.UploadedKeys.Contains(targetStorageLocation);
        return exists;
    }
    
    private bool SkipStoragePolicyCheck(int customerId)
    {
        var customerSpecific = engineSettings.GetCustomerSettings(customerId);
        return customerSpecific.NoStoragePolicyCheck;
    }
}