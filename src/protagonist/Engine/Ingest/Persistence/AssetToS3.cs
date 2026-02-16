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
    /// <param name="context">Ingestion context containing the <see cref="Asset"/> to be copied</param>
    /// <param name="verifySize">if True, size is validated that it does not exceed allowed size.</param>
    /// <param name="customerOriginStrategy"><see cref="CustomerOriginStrategy"/> to use to fetch item.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <returns><see cref="AssetFromOrigin"/> containing new location, size etc</returns>
    Task<AssetFromOrigin> CopyOriginToStorage(ObjectInBucket destination, IngestionContext context, bool verifySize,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default);
}

public interface IAdjunctToS3
{
    /// <summary>
    /// Copy adjunct from Origin to DLCS storage.
    /// Configuration determines if this is a direct S3-S3 copy, or S3-disk-S3.
    /// </summary>
    /// <param name="destination"><see cref="ObjectInBucket"/> where file is to copied to</param>
    /// <param name="context">Ingestion context containing the <see cref="Asset"/> to be copied</param>
    /// <param name="verifySize">if True, size is validated that it does not exceed allowed size.</param>
    /// <param name="customerOriginStrategy"><see cref="CustomerOriginStrategy"/> to use to fetch item.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <returns><see cref="AssetFromOrigin"/> containing new location, size etc</returns>
    Task<AssetFromOrigin> CopyAdjunctToStorage(ObjectInBucket destination, AdjunctIngestionContext context,
        bool verifySize,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default);
}

/// <summary>
/// Class for copying asset from origin to S3 bucket.
/// </summary>
public class AssetToS3(
    IAssetToDisk assetToDisk,
    IOptionsMonitor<EngineSettings> engineSettings,
    IStorageRepository storageRepository,
    IBucketWriter bucketWriter,
    IFileSystem fileSystem,
    ILogger<AssetToS3> logger)
    : AssetMoverBase(storageRepository), IAssetToS3, IAdjunctToS3
{
    private readonly EngineSettings engineSettings = engineSettings.CurrentValue;

    public async Task<AssetFromOrigin> CopyOriginToStorage(ObjectInBucket destination, IngestionContext context,
        bool verifySize,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var copyResult = await DoAssetCopy(destination, context, verifySize, customerOriginStrategy, cancellationToken);
        stopwatch.Stop();
        logger.LogDebug("Copied asset {AssetId} in {Elapsed}ms using {OriginStrategy}",
            context.Asset.Id, stopwatch.ElapsedMilliseconds, customerOriginStrategy.Strategy);

        return copyResult;
    }

    public async Task<AssetFromOrigin> CopyAdjunctToStorage(ObjectInBucket destination, AdjunctIngestionContext context,
        bool verifySize,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var copyResult =
            await DoAdjunctCopy(destination, context, verifySize, customerOriginStrategy, cancellationToken);
        stopwatch.Stop();
        logger.LogDebug("Copied adjunct {AssetId} in {Elapsed}ms using {OriginStrategy}",
            context.Asset.Id, stopwatch.ElapsedMilliseconds, customerOriginStrategy.Strategy);

        return copyResult;
    }

    private async Task<AssetFromOrigin> DoAdjunctCopy(ObjectInBucket destination, AdjunctIngestionContext context,
        bool verifySize, CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken)
    {
        if (ShouldCopyBucketToBucket(customerOriginStrategy))
        {
            // We have direct bucket access so can copy directly using SDK
            return await CopyAdjunctBucketToBucket(context, destination, verifySize, cancellationToken);
        }

        // We don't have direct bucket access; or it's a non-S3 origin so copy S3->Disk->S3 
        return await IndirectAdjunctCopyBucketToBucket(context, destination, verifySize, customerOriginStrategy,
            cancellationToken);
    }

    private async Task<AssetFromOrigin> DoAssetCopy(ObjectInBucket destination, IngestionContext context,
        bool verifySize,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken)
    {
        if (ShouldCopyBucketToBucket(customerOriginStrategy))
        {
            // We have direct bucket access so can copy directly using SDK
            return await CopyAssetBucketToBucket(context, destination, verifySize, cancellationToken);
        }

        // We don't have direct bucket access; or it's a non-S3 origin so copy S3->Disk->S3 
        return await IndirectAssetCopyBucketToBucket(context, destination, verifySize, customerOriginStrategy,
            cancellationToken);
    }

    private static bool ShouldCopyBucketToBucket(CustomerOriginStrategy customerOriginStrategy)
        => customerOriginStrategy is { Strategy: OriginStrategyType.S3Ambient };

    private async Task<AssetFromOrigin> CopyAdjunctBucketToBucket(AdjunctIngestionContext context,
        ObjectInBucket destination,
        bool verifySize, CancellationToken cancellationToken)
    {
        // Origin being null should prevent flow from reaching this
        Debug.Assert(context.Adjunct.Origin != null, "context.Adjunct.Origin != null");

        var copyResult = await CopyBucketToBucket(context.Adjunct.Origin, destination, context.Asset.Customer,
            verifySize,
            context.PreIngestionAssetSize, $"Adjunct {context.Adjunct.Id} for asset with id {context.Asset.Id}",
            cancellationToken);

        var adjunctFromOrigin = new AdjunctFromOrigin(context.Adjunct.Id, context.AssetId, copyResult.Size ?? 0,
            destination.GetS3Uri().ToString(), null);

        if (copyResult.Result == LargeObjectStatus.FileTooLarge)
        {
            adjunctFromOrigin.FileTooLarge();
        }

        return adjunctFromOrigin;
    }

    private async Task<AssetFromOrigin> CopyAssetBucketToBucket(IngestionContext context, ObjectInBucket destination,
        bool verifySize,
        CancellationToken cancellationToken)
    {
        var assetId = context.Asset.Id;

        // Origin being null should prevent flow from reaching this
        Debug.Assert(context.Asset.Origin != null, "context.Asset.Origin != null");

        var copyResult = await CopyBucketToBucket(context.Asset.Origin, destination, context.Asset.Customer, verifySize,
            context.PreIngestionAssetSize, $"Asset with id {context.Asset.Id}", cancellationToken);

        var assetFromOrigin = new AssetFromOrigin(assetId, copyResult.Size ?? 0, destination.GetS3Uri().ToString(),
            context.Asset.MediaType);

        if (copyResult.Result == LargeObjectStatus.FileTooLarge)
        {
            assetFromOrigin.FileTooLarge();
        }

        return assetFromOrigin;
    }

    private async Task<LargeObjectCopyResult> CopyBucketToBucket(string origin, ObjectInBucket destination,
        int customerId, bool verifySize, long preIngestionSize, string itemDescription,
        CancellationToken cancellationToken)
    {
        var source = RegionalisedObjectInBucket.Parse(origin);
        if (source == null)
        {
            // TODO - better error type
            logger.LogError("Unable to parse ingest origin {Origin} to ObjectInBucket", origin);
            throw new InvalidOperationException(
                $"Unable to parse ingest origin {origin} to ObjectInBucket");
        }

        logger.LogDebug("Copying {Item} directly from bucket to bucket. {Source} - {Dest}", itemDescription,
            source.GetS3Uri(), destination.GetS3Uri());

        // copy S3-S3
        Func<long, Task<bool>>? sizeVerifier =
            verifySize ? assetSize => VerifyFileSize(customerId, assetSize, preIngestionSize) : null;
        var copyResult =
            await bucketWriter.CopyLargeObject(source, destination, verifySize: sizeVerifier, token: cancellationToken);

        if (copyResult.Result is not LargeObjectStatus.Success and not LargeObjectStatus.FileTooLarge)
        {
            throw new ApplicationException(
                $"Failed to copy {itemDescription} directly from '{origin}' to {destination.GetS3Uri()}. Result: {copyResult.Result}");
        }

        return copyResult;
    }

    private async Task<AssetFromOrigin> IndirectAdjunctCopyBucketToBucket(AdjunctIngestionContext context,
        ObjectInBucket destination, bool verifySize, CustomerOriginStrategy customerOriginStrategy,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Copying {Adjunct} for Asset {Asset} indirectly from bucket to bucket. {Source} - {Dest}",
            context.Adjunct.Id, context.Asset.Id, context.Asset.Origin, destination.GetS3Uri());

        string? downloadedFile = null;
        try
        {
            var diskDestination = GetDestination(context.Asset.Id, context.Adjunct.Id);

            var adjunctOnDisk = await assetToDisk.CopyAdjunctToLocalDisk(context, diskDestination, verifySize,
                customerOriginStrategy, cancellationToken);

            if (adjunctOnDisk.FileExceedsAllowance)
            {
                return adjunctOnDisk;
            }

            logger.LogDebug("Copied adjunct {Adjunct} for asset '{Asset}' to disk, copying to bucket..", context.Adjunct.Id, context.Asset.Id);
            var success = await bucketWriter.WriteFileToBucket(destination, adjunctOnDisk.Location,
                adjunctOnDisk.ContentType, cancellationToken);
            downloadedFile = adjunctOnDisk.Location;

            if (!success)
            {
                throw new ApplicationException(
                    $"Failed to copy adjunct {context.Adjunct.Id} for asset {context.Asset.Id} indirectly from '{context.Asset.Origin}' to {destination}");
            }

            return new AssetFromOrigin(context.Asset.Id, adjunctOnDisk.AssetSize, destination.GetS3Uri().ToString(),
                adjunctOnDisk.ContentType);
        }
        finally
        {
            if (!string.IsNullOrEmpty(downloadedFile))
            {
                fileSystem.DeleteFile(downloadedFile);
            }
        }
    }

    private async Task<AssetFromOrigin> IndirectAssetCopyBucketToBucket(IngestionContext context,
        ObjectInBucket destination,
        bool verifySize, CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken)
    {
        logger.LogDebug("Copying {Asset} indirectly from bucket to bucket. {Source} - {Dest}",
            context.Asset.Id, context.Asset.Origin, destination.GetS3Uri());

        string? downloadedFile = null;
        try
        {
            var diskDestination = GetDestination(context.Asset.Id);

            var assetOnDisk = await assetToDisk.CopyAssetToLocalDisk(context, diskDestination, verifySize,
                customerOriginStrategy, cancellationToken);

            if (assetOnDisk.FileExceedsAllowance)
            {
                return assetOnDisk;
            }

            logger.LogDebug("Copied '{Asset}' to disk, copying to bucket..", context.Asset.Id);
            var success = await bucketWriter.WriteFileToBucket(destination, assetOnDisk.Location,
                assetOnDisk.ContentType, cancellationToken);
            downloadedFile = assetOnDisk.Location;

            if (!success)
            {
                throw new ApplicationException(
                    $"Failed to copy {context.Asset.Id} indirectly from '{context.Asset.Origin}' to {destination}");
            }

            return new AssetFromOrigin(context.Asset.Id, assetOnDisk.AssetSize, destination.GetS3Uri().ToString(),
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

    private string GetDestination(AssetId assetId, string? adjunctId = null)
    {
        var diskDestination = adjunctId is { Length: > 0 }
            ? TemplatedFolders.GenerateFolderTemplateForAdjunct(engineSettings.DownloadTemplate, assetId, adjunctId)
            : TemplatedFolders.GenerateFolderTemplate(engineSettings.DownloadTemplate, assetId);

        fileSystem.CreateDirectory(diskDestination);
        return diskDestination;
    }
}
