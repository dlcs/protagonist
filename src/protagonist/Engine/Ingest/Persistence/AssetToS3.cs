using System.Diagnostics;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.FileSystem;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Storage;
using DLCS.Model.Templates;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Persistence;

public interface IAssetToS3
{
    /// <summary>
    /// Copy asset from Origin to DLCS storage.
    /// Configuration determines if this is a direct S3-S3 copy, or S3-disk-S3.
    /// </summary>
    /// <param name="destination"><see cref="ObjectInBucket"/> where file is to copied to</param>
    /// <param name="context">Ingestion context containing the <see cref="Asset"/> to be copied.</param>
    /// <param name="verifySize">if True, size is validated that it does not exceed allowed size.</param>
    /// <param name="customerOriginStrategy"><see cref="CustomerOriginStrategy"/> to use to fetch item.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <returns><see cref="AssetFromOrigin"/> containing new location, size etc</returns>
    Task<AssetFromOrigin> CopyOriginToStorage(ObjectInBucket destination, IngestionContext context, bool verifySize,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default);
}

/// <summary>
/// Class for copying asset from origin to S3 bucket.
/// </summary>
public class AssetToS3 : AssetMoverBase, IAssetToS3
{
    private readonly IAssetToDisk assetToDisk;
    private readonly IBucketWriter bucketWriter;
    private readonly IFileSystem fileSystem;
    private readonly EngineSettings engineSettings;
    private readonly ILogger<AssetToS3> logger;

    public AssetToS3(
        IAssetToDisk assetToDisk,
        IOptionsMonitor<EngineSettings> engineSettings,
        IStorageRepository storageRepository,
        IBucketWriter bucketWriter, 
        IFileSystem fileSystem,
        ILogger<AssetToS3> logger) : base(storageRepository)
    {
        this.assetToDisk = assetToDisk;
        this.engineSettings = engineSettings.CurrentValue;
        this.logger = logger;
        this.bucketWriter = bucketWriter;
        this.fileSystem = fileSystem;
    }
    
    public async Task<AssetFromOrigin> CopyOriginToStorage(ObjectInBucket destination, IngestionContext context, bool verifySize,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var copyResult = await DoCopy(destination, context, verifySize, customerOriginStrategy, cancellationToken);
        stopwatch.Stop();
        logger.LogDebug("Copied asset {AssetId} in {Elapsed}ms using {OriginStrategy}", 
            context.Asset.Id, stopwatch.ElapsedMilliseconds, customerOriginStrategy.Strategy);
        
        return copyResult;
    }

    private async Task<AssetFromOrigin> DoCopy(ObjectInBucket destination, IngestionContext context, bool verifySize,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken)
    {
        if (ShouldCopyBucketToBucket(customerOriginStrategy))
        {
            // We have direct bucket access so can copy directly using SDK
            return await CopyBucketToBucket(context, destination, verifySize, cancellationToken);
        }

        // We don't have direct bucket access; or it's a non-S3 origin so copy S3->Disk->S3 
        return await IndirectCopyBucketToBucket(context, destination, verifySize, customerOriginStrategy,
            cancellationToken);
    }

    private bool ShouldCopyBucketToBucket(CustomerOriginStrategy customerOriginStrategy)
        => customerOriginStrategy is { Strategy: OriginStrategyType.S3Ambient };

    private async Task<AssetFromOrigin> CopyBucketToBucket(IngestionContext context, ObjectInBucket destination, bool verifySize,
        CancellationToken cancellationToken)
    {
        var assetId = context.Asset.Id;
        var source = RegionalisedObjectInBucket.Parse(context.Asset.GetIngestOrigin());

        if (source == null)
        {
            // TODO - better error type
            logger.LogError("Unable to parse ingest origin {Origin} to ObjectInBucket", context.Asset.GetIngestOrigin());
            throw new InvalidOperationException(
                $"Unable to parse ingest origin {context.Asset.GetIngestOrigin()} to ObjectInBucket");
        }
        
        logger.LogDebug("Copying asset '{AssetId}' directly from bucket to bucket. {Source} - {Dest}", context.Asset.Id,
            source.GetS3Uri(), destination.GetS3Uri());

        // copy S3-S3
        Func<long, Task<bool>>? sizeVerifier = verifySize ? assetSize => VerifyFileSize(assetId, assetSize, context.PreIngestionAssetSize) : null;
        var copyResult =
            await bucketWriter.CopyLargeObject(source, destination, verifySize: sizeVerifier, token: cancellationToken);

        if (copyResult.Result is not LargeObjectStatus.Success and not LargeObjectStatus.FileTooLarge)
        {
            throw new ApplicationException(
                $"Failed to copy timebased asset {context.Asset.Id} directly from '{context.Asset.GetIngestOrigin()}' to {destination.GetS3Uri()}. Result: {copyResult.Result}");
        }

        var assetFromOrigin = new AssetFromOrigin(assetId, copyResult.Size ?? 0, destination.GetS3Uri().ToString(),
            context.Asset.MediaType);
        
        if (copyResult.Result == LargeObjectStatus.FileTooLarge)
        {
            assetFromOrigin.FileTooLarge();
        }

        return assetFromOrigin;
    }

    private async Task<AssetFromOrigin> IndirectCopyBucketToBucket(IngestionContext context, ObjectInBucket destination, 
        bool verifySize, CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken)
    {
        logger.LogDebug("Copying asset '{AssetId}' indirectly from bucket to bucket. {Source} - {Dest}",
            context.Asset.Id,
            context.Asset.GetIngestOrigin(), destination.GetS3Uri());
        var assetId = context.Asset.Id;
        string? downloadedFile = null;

        try
        {
            var diskDestination = GetDestination(assetId);
            var assetOnDisk = await assetToDisk.CopyAssetToLocalDisk(context, diskDestination, verifySize,
                customerOriginStrategy, cancellationToken);

            if (assetOnDisk.FileExceedsAllowance)
            {
                return assetOnDisk;
            }

            logger.LogDebug("Copied asset '{AssetId}' to disk, copying to bucket..", context.Asset.Id);
            var success = await bucketWriter.WriteFileToBucket(destination, assetOnDisk.Location,
                assetOnDisk.ContentType, cancellationToken);
            downloadedFile = assetOnDisk.Location;
            
            if (!success)
            {
                throw new ApplicationException(
                    $"Failed to copy timebased asset {assetId} indirectly from '{context.Asset.GetIngestOrigin()}' to {destination}");
            }

            return new AssetFromOrigin(assetId, assetOnDisk.AssetSize, destination.GetS3Uri().ToString(),
                assetOnDisk.ContentType);
        }
        finally
        {
            if (!string.IsNullOrEmpty(downloadedFile))
            {
                fileSystem.DeleteFile(downloadedFile);
            }
        }
    }
    
    private string GetDestination(AssetId assetId)
    {
        var diskDestination = TemplatedFolders.GenerateFolderTemplate(engineSettings.DownloadTemplate, assetId);
        fileSystem.CreateDirectory(diskDestination);
        return diskDestination;
    }
}