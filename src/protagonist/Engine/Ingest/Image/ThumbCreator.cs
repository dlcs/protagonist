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
        var expectedSizes = asset.GetAvailableThumbSizes(out var maxDimensions, true);
        if (expectedSizes.Count == 0)
        {
            logger.LogDebug("No expected thumb sizes for {AssetId}, aborting", assetId);
            return 0;
        }

        var imageShape = expectedSizes[0].GetShape();
        var maxAvailableThumb = Size.Square(maxDimensions.maxBoundedSize);
        var thumbnailSizes = new ThumbnailSizes(thumbsToProcess.Count);
        var processedWidths = new List<int>(thumbsToProcess.Count);
        
        using var processLock = await asyncLocker.LockAsync($"create:{assetId}");

        // First is always largest
        bool processingLargest = true;
        foreach (var thumbCandidate in thumbsToProcess)
        {
            // Safety check for duplicate
            if (processedWidths.Contains(thumbCandidate.Width))
            {
                logger.LogDebug("Thumbnail {Width},{Height} has already been processed for asset {AssetId}",
                    thumbCandidate.Width, thumbCandidate.Height, assetId);
                continue;
            }

            var thumb = GetThumbnailSize(thumbCandidate, imageShape, expectedSizes, assetId);
            
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

    /// <summary>
    /// Find matching size from pre-calculated thumbs. We use these rather than sizes returned by image-processor to
    /// avoid rounding issues
    /// </summary>
    private Size GetThumbnailSize(ImageOnDisk imageOnDisk, ImageShape imageShape, IEnumerable<Size> expectedSizes,
        AssetId assetId)
    {
        try
        {
            return imageShape == ImageShape.Landscape
                ? expectedSizes.Single(s => s.Width == imageOnDisk.Width)
                : expectedSizes.Single(s => s.Height == imageOnDisk.Height);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError("Unable to find expected thumbnail size {Width},{Height} for asset {AssetId}. {Path}",
                imageOnDisk.Width, imageOnDisk.Height, assetId, imageOnDisk.Path);
            throw new ApplicationException(
                $"Unable to find expected thumbnail size {imageOnDisk.Width},{imageOnDisk.Height}", ex);
        }
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