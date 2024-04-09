using DLCS.AWS.S3;
using DLCS.Core;
using DLCS.Core.Threading;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository.Assets;
using DLCS.Repository.Assets.Thumbs;
using IIIF;

namespace Engine.Ingest.Image;

public class ThumbCreator : ThumbsManager, IThumbCreator
{
    private readonly ILogger<ThumbCreator> logger;
    private readonly AsyncKeyedLock asyncLocker = new();

    public ThumbCreator(
        IBucketWriter bucketWriter,
        IStorageKeyGenerator storageKeyGenerator,
        ILogger<ThumbCreator> logger) : base(bucketWriter, storageKeyGenerator)
    {
        this.logger = logger;
    }

    public async Task<int> CreateNewThumbs(Asset asset, IReadOnlyList<ImageOnDisk> thumbsToProcess)
    {
        var assetId = asset.Id;

        if (thumbsToProcess.Count == 0)
        {
            logger.LogDebug("No thumbs to process for {AssetId}, aborting", assetId);
            return 0;
        }

        var maxAvailableThumb = GetMaxThumbnailSize(asset, thumbsToProcess);
        var thumbnailSizes = new ThumbnailSizes(thumbsToProcess.Count);
        var processedWidths = new List<int>(thumbsToProcess.Count);
        
        using var processLock = await asyncLocker.LockAsync($"create:{assetId}");

        // First is always largest
        bool processingLargest = true;
        foreach (var thumbCandidate in thumbsToProcess)
        {
            if (thumbCandidate.Width > asset.Width || thumbCandidate.Height > asset.Height) continue;
            
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
            
            await UploadThumbs(processingLargest, assetId, thumbCandidate, thumb, isOpen);

            processingLargest = false;
            processedWidths.Add(thumbCandidate.Width);
        }
            
        await CreateSizesJson(assetId, thumbnailSizes);
        return thumbnailSizes.Count;
    }
    
    private Size GetMaxThumbnailSize(Asset asset, IReadOnlyList<ImageOnDisk> thumbsToProcess)
    {
        if (asset.MaxUnauthorised == 0) return new Size(0, 0);

        foreach (var thumb in thumbsToProcess.OrderByDescending(x => Math.Max(x.Height, x.Width)))
        {
            if ((asset.MaxUnauthorised ?? -1) == -1) return new Size(thumb.Width, thumb.Height);

            if (asset.MaxUnauthorised > Math.Max(thumb.Width, thumb.Height)) return new Size(thumb.Width, thumb.Height);
        }
        
        return new Size(0, 0);
    }

    private async Task UploadThumbs(bool processingLargest, AssetId assetId, ImageOnDisk thumbCandidate, Size thumb,
        bool isOpen)
    {
        if (processingLargest)
        {
            // The largest thumb always goes to low.jpg as well as the 'normal' place
            var lowKey = StorageKeyGenerator.GetLargestThumbnailLocation(assetId);
            await BucketWriter.WriteFileToBucket(lowKey, thumbCandidate.Path, MIMEHelper.JPEG);
        }

        var thumbKey = StorageKeyGenerator.GetThumbnailLocation(assetId, thumb.MaxDimension, isOpen);
        await BucketWriter.WriteFileToBucket(thumbKey, thumbCandidate.Path, MIMEHelper.JPEG);
    }
}