using DLCS.AWS.S3;
using DLCS.Core;
using DLCS.Core.Threading;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using Engine.Data;
using IIIF;
using IIIF.ImageApi;
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

        // Images processed Largest->Smallest. This is how they are stored in S3 + DB as it saves reordering on read 
        var orderedThumbs = thumbsToProcess.OrderByDescending(i => Math.Max(i.Height, i.Width)).ToList();

        var maxAvailableThumb = GetMaxAvailableThumbnailSize(asset, orderedThumbs);
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
    
    private static Size GetMaxAvailableThumbnailSize(Asset asset, List<ImageOnDisk> orderedThumbsToProcess)
    {
        if (asset.MaxUnauthorised == 0) return new Size(0, 0);
        if ((asset.MaxUnauthorised ?? -1) == -1) return new Size(orderedThumbsToProcess[0].Width, orderedThumbsToProcess[0].Height);

        foreach (var thumb in orderedThumbsToProcess)
        {
            if (asset.MaxUnauthorised >= Math.Max(thumb.Width, thumb.Height)) return new Size(thumb.Width, thumb.Height);
        }

        return new Size(0, 0);
    }

    private async Task UploadThumbs(AssetId assetId, ImageOnDisk thumbCandidate, Size thumb, bool isOpen)
    {
        var thumbKey = storageKeyGenerator.GetThumbnailLocation(assetId, thumb.MaxDimension, isOpen);
        await bucketWriter.WriteFileToBucket(thumbKey, thumbCandidate.Path, MIMEHelper.JPEG);
    }
    
    private async Task CreateSizesJson(Asset asset, ThumbnailSizes thumbnailSizes)
    {
        // NOTE - this data is read via AssetApplicationMetadataX.GetThumbsMetadata
        var serializedThumbnailSizes = JsonConvert.SerializeObject(thumbnailSizes);
        var sizesDest = storageKeyGenerator.GetThumbsSizesJsonLocation(asset.Id);
        await bucketWriter.WriteToBucket(sizesDest, serializedThumbnailSizes, "application/json");
        asset.UpsertApplicationMetadata(AssetApplicationMetadataTypes.ThumbSizes, serializedThumbnailSizes);
    }
}
