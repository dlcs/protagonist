using DLCS.AWS.S3;
using DLCS.Core;
using DLCS.Core.Threading;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using Engine.Data;
using IIIF;
using Newtonsoft.Json;

namespace Engine.Ingest.Image;

public class ThumbCreator(
    IBucketWriter bucketWriter,
    IStorageKeyGenerator storageKeyGenerator,
    ILogger<ThumbCreator> logger)
    : IThumbCreator
{
    private readonly AsyncKeyedLock asyncLocker = new();

    public async Task<int> CreateNewThumbs(Asset asset, IReadOnlyList<ImageOnDisk> thumbsToProcess)
    {
        var assetId = asset.Id;

        if (thumbsToProcess.Count == 0)
        {
            logger.LogDebug("No thumbs to process for {AssetId}, aborting", assetId);
            return 0;
        }
        
        logger.LogDebug("Creating {ThumbsCount} thumbs for {AssetId}", thumbsToProcess.Count, assetId);

        // Images processed Largest->Smallest. This is how they are stored in S3 + DB as it saves reordering on read 
        var orderedThumbs = thumbsToProcess.OrderByDescending(i => Math.Max(i.Height, i.Width)).ToList();

        var maxAvailableThumb = GetMaxOpenThumbnailSize(asset, orderedThumbs);
        logger.LogTrace("Max available thumbnail size for {AssetId} is {MaxAvailableThumb}", assetId, maxAvailableThumb);
        var thumbnailSizes = new ThumbnailSizes(thumbsToProcess.Count);
        var processedWidths = new List<int>(thumbsToProcess.Count);
        
        using var processLock = await asyncLocker.LockAsync($"create:{assetId}");

        foreach (var thumbCandidate in orderedThumbs)
        {
            // Safety check for duplicate
            if (processedWidths.Contains(thumbCandidate.Width))
            {
                logger.LogDebug("Thumbnail {Width},{Height} has already been processed for asset {AssetId}",
                    thumbCandidate.Width, thumbCandidate.Height, assetId);
                continue;
            }

            var thumb = new Size(thumbCandidate.Width, thumbCandidate.Height);
            
            bool isOpen;
            if (thumb.IsConfinedWithin(maxAvailableThumb))
            {
                thumbnailSizes.AddOpen(thumb);
                isOpen = true;
            }
            else
            {
                thumbnailSizes.AddAuth(thumb);
                isOpen = false;
            }
            
            await UploadThumbs(assetId, thumbCandidate, thumb, isOpen);

            processedWidths.Add(thumbCandidate.Width);
        }
            
        await CreateSizesJson(asset, thumbnailSizes);
        return thumbnailSizes.Count;
    }
    
    private Size GetMaxOpenThumbnailSize(Asset asset, IReadOnlyList<ImageOnDisk> orderedThumbsToProcess)
    {
        // The effective max dimension for open thumbnails
        var effectiveMax = GetEffectiveOpenMaxDimension(asset);
        logger.LogTrace("Effective max open dimension for {AssetId} is {MaxDimension}", asset.Id, effectiveMax);

        // 0 with roles means "no open", without roles means "all open"
        if (effectiveMax == 0)
        {
            if (asset.HasRoles)
            {
                return new Size(0, 0);
            }

            var largestImageOnDisk = orderedThumbsToProcess[0];
            return new Size(largestImageOnDisk.Width, largestImageOnDisk.Height);
        }

        var thumb = orderedThumbsToProcess.FirstOrDefault(thumb => effectiveMax >= Math.Max(thumb.Width, thumb.Height));
        return thumb == null ? new Size(0, 0) : new Size(thumb.Width, thumb.Height);
    }
    
    private static int GetEffectiveOpenMaxDimension(Asset asset)
    {
        // If no role, only maxWidth can restrict (openFullMax is ignored)
        if (!asset.HasRoles) return asset.MaxWidth ?? 0;
            
        // If OpenFullMax == 0 then there are no "open" thumbs, return 0
        if ((asset.OpenFullMax ?? 0) == 0) return 0;

        // We have an OpenFullMax value, if we also have MaxWidth return the smallest of that an OpenFullMax
        return asset.MaxWidth > 0
            ? Math.Min(asset.MaxWidth.Value, asset.OpenFullMax!.Value)
            : asset.OpenFullMax ?? 0;
    }

    private async Task UploadThumbs(AssetId assetId, ImageOnDisk thumbCandidate, Size thumb, bool isOpen)
    {
        var thumbKey = storageKeyGenerator.GetThumbnailLocation(assetId, thumb.MaxDimension, isOpen);
        logger.LogTrace("Saving thumbnail {ThumbnailKey}", thumbKey);
        await bucketWriter.WriteFileToBucket(thumbKey, thumbCandidate.Path, MIMEHelper.JPEG);
    }
    
    private async Task CreateSizesJson(Asset asset, ThumbnailSizes thumbnailSizes)
    {
        // NOTE - this data is read via AssetApplicationMetadataX.GetThumbsMetadata
        var serializedThumbnailSizes = JsonConvert.SerializeObject(thumbnailSizes);
        var sizesDest = storageKeyGenerator.GetThumbsSizesJsonLocation(asset.Id);
        logger.LogTrace("Saving sizes json {SizesKey}", sizesDest);
        await bucketWriter.WriteToBucket(sizesDest, serializedThumbnailSizes, "application/json");
        asset.UpsertApplicationMetadata(AssetApplicationMetadataTypes.ThumbSizes, serializedThumbnailSizes);
    }
}
