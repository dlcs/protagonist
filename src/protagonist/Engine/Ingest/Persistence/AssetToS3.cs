using System.Diagnostics;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.FileSystem;
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
    /// Copy <see cref="IOriginItem"/> provided by the supplied <paramref name="context"/> from Origin to DLCS storage.
    /// Configuration determines if this is a direct S3-S3 copy, or S3-disk-S3.
    /// When <paramref name="validator"/> is provided the copy always goes via local disk so the validator can inspect
    /// the file; the S3 write is skipped if the validator returns false.
    /// </summary>
    /// <param name="destination"><see cref="ObjectInBucket"/> where file is to copied to</param>
    /// <param name="context">Ingestion context containing the <see cref="IOriginItem"/> to be copied</param>
    /// <param name="verifySize">if True, size is validated that it does not exceed allowed size.</param>
    /// <param name="customerOriginStrategy"><see cref="CustomerOriginStrategy"/> to use to fetch item.</param>
    /// <param name="validator">Optional callback invoked with the local file path after download but before S3 upload.
    /// Return null to allow the upload; return an error message string to abort it.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <returns><see cref="AssetFromOrigin"/> containing new location, size etc</returns>
    Task<AssetFromOrigin> CopyOriginToStorage(ObjectInBucket destination, IngestionContext context, bool verifySize,
        CustomerOriginStrategy customerOriginStrategy,
        Func<string, CancellationToken, Task<string?>>? validator = null,
        CancellationToken cancellationToken = default);
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
    : AssetMoverBase(storageRepository), IAssetToS3
{
    private readonly EngineSettings engineSettings = engineSettings.CurrentValue;

    public async Task<AssetFromOrigin> CopyOriginToStorage(ObjectInBucket destination, IngestionContext context,
        bool verifySize, CustomerOriginStrategy customerOriginStrategy,
        Func<string, CancellationToken, Task<string?>>? validator = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var copyResult = await DoItemCopy(destination, context, verifySize, customerOriginStrategy, validator,
            cancellationToken);
        stopwatch.Stop();
        logger.LogDebug("Copied item {Item} in {Elapsed}ms using {OriginStrategy}",
            context.GetOriginItem().Identifier(), stopwatch.ElapsedMilliseconds, customerOriginStrategy.Strategy);

        return copyResult;
    }

    private async Task<AssetFromOrigin> DoItemCopy(ObjectInBucket destination, IngestionContext context,
        bool verifySize, CustomerOriginStrategy customerOriginStrategy,
        Func<string, CancellationToken, Task<string?>>? validator, CancellationToken cancellationToken)
    {
        if (validator == null && ShouldCopyBucketToBucket(customerOriginStrategy))
        {
            // We have direct bucket access so can copy directly using SDK
            return await CopyAssetBucketToBucket(context, destination, verifySize, cancellationToken);
        }

        // We don't have direct bucket access; or it's a non-S3 origin so copy S3->Disk->S3.
        // Also forced when a validator is provided, so the file is available on disk for inspection.
        return await IndirectAssetCopyBucketToBucket(context, destination, verifySize, customerOriginStrategy,
            validator, cancellationToken);
    }

    private static bool ShouldCopyBucketToBucket(CustomerOriginStrategy customerOriginStrategy)
        => customerOriginStrategy is { Strategy: OriginStrategyType.S3Ambient };

    private async Task<AssetFromOrigin> CopyAssetBucketToBucket(IngestionContext context, ObjectInBucket destination,
        bool verifySize, CancellationToken cancellationToken)
    {
        var item = context.GetOriginItem();

        // Origin being null should prevent flow from reaching this
        Debug.Assert(item.Origin != null, "item.Origin != null");

        var copyResult = await CopyBucketToBucket(item, destination, context.Asset.Customer, verifySize,
            context.PreIngestionAssetSize, cancellationToken);

        var assetFromOrigin = context.CreateAssetFromOrigin(copyResult.Size ?? 0, destination.GetS3Uri().ToString(),
            context.GetMediaType());

        if (copyResult.Result == LargeObjectStatus.FileTooLarge)
        {
            assetFromOrigin.FileTooLarge();
        }

        return assetFromOrigin;
    }

    private async Task<LargeObjectCopyResult> CopyBucketToBucket(IOriginItem originItem, ObjectInBucket destination,
        int customerId, bool verifySize, long preIngestionSize, CancellationToken cancellationToken)
    {
        var source = RegionalisedObjectInBucket.Parse(originItem.Origin!);
        if (source == null)
        {
            // TODO - better error type
            logger.LogError("Unable to parse ingest origin {Origin} to ObjectInBucket", originItem.Origin);
            throw new InvalidOperationException(
                $"Unable to parse ingest origin {originItem.Origin} to ObjectInBucket");
        }

        logger.LogDebug("Copying {Item} directly from bucket to bucket. {Source} - {Dest}", originItem.Identifier(),
            source.GetS3Uri(), destination.GetS3Uri());

        // copy S3-S3
        Func<long, Task<bool>>? sizeVerifier =
            verifySize ? assetSize => VerifyFileSize(customerId, assetSize, preIngestionSize) : null;
        var copyResult =
            await bucketWriter.CopyLargeObject(source, destination, verifySize: sizeVerifier, token: cancellationToken);

        if (copyResult.Result is not LargeObjectStatus.Success and not LargeObjectStatus.FileTooLarge)
        {
            throw new ApplicationException(
                $"Failed to copy {originItem.Identifier()} directly from '{originItem.Origin}' to {destination.GetS3Uri()}. Result: {copyResult.Result}");
        }

        return copyResult;
    }

    private async Task<AssetFromOrigin> IndirectAssetCopyBucketToBucket(IngestionContext context,
        ObjectInBucket destination,
        bool verifySize, CustomerOriginStrategy customerOriginStrategy,
        Func<string, CancellationToken, Task<string?>>? validator,
        CancellationToken cancellationToken)
    {
        var item = context.GetOriginItem();

        logger.LogDebug("Copying {Item} indirectly from bucket to bucket. {Source} - {Dest}",
            item.Identifier(), item.Origin, destination.GetS3Uri());

        string? downloadedFile = null;
        try
        {
            var diskDestination = GetDestination(context);

            var itemOnDisk = await assetToDisk.CopyItemToLocalDisk(context, diskDestination, verifySize,
                customerOriginStrategy, cancellationToken);

            if (itemOnDisk.FileExceedsAllowance)
            {
                return itemOnDisk;
            }

            downloadedFile = itemOnDisk.Location;

            if (validator != null)
            {
                var validationError = await validator(itemOnDisk.Location, cancellationToken);
                if (validationError != null)
                {
                    throw new InvalidOperationException(validationError);
                }
            }

            logger.LogDebug("Copied '{Item}' to disk, copying to bucket..", item.Identifier());

            var success = await bucketWriter.WriteFileToBucket(destination, itemOnDisk.Location,
                itemOnDisk.ContentType, cancellationToken);

            if (!success)
            {
                throw new ApplicationException(
                    $"Failed to copy {item.Identifier()} indirectly from '{item.Origin}' to {destination}");
            }

            return context.CreateAssetFromOrigin(itemOnDisk.AssetSize, destination.GetS3Uri().ToString(),
                itemOnDisk.ContentType);
        }
        finally
        {
            if (!string.IsNullOrEmpty(downloadedFile))
            {
                fileSystem.DeleteFile(downloadedFile);
            }
        }
    }

    private string GetDestination(IngestionContext context)
    {
        var assetId = context.AssetId;
        var adjunctId = context is AdjunctIngestionContext adjContext ? adjContext.Adjunct.Id : null;

        var diskDestination = adjunctId is { Length: > 0 }
            ? TemplatedFolders.GenerateFolderTemplateForAdjunct(engineSettings.DownloadTemplate, assetId, adjunctId)
            : TemplatedFolders.GenerateFolderTemplate(engineSettings.DownloadTemplate, assetId);

        fileSystem.CreateDirectory(diskDestination);
        return diskDestination;
    }
}
