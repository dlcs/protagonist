using DLCS.AWS.S3;
using DLCS.Core;
using DLCS.Core.Threading;
using DLCS.Model.Assets;
using DLCS.Repository.Assets;
using DLCS.Repository.Assets.Thumbs;
using IIIF;

namespace Engine.Ingest.Image;

public class ThumbCreator : ThumbsManager, IThumbCreator
{
    private readonly AsyncKeyedLock asyncLocker = new();

    public ThumbCreator(
        IBucketWriter bucketWriter,
        IStorageKeyGenerator storageKeyGenerator) : base(bucketWriter, storageKeyGenerator)
    {
    }

    public async Task CreateNewThumbs(Asset asset, IReadOnlyList<ImageOnDisk> thumbsToProcess)
    {
        if (thumbsToProcess.Count == 0) return;
        var assetId = asset.Id;
            
        using var processLock = await asyncLocker.LockAsync($"create:{assetId}");
        var thumbnailSizes = new ThumbnailSizes(thumbsToProcess.Count);
        var maxAvailableThumb = GetMaxAvailableThumb(asset, asset.FullThumbnailPolicy);
            
        // this is the largest thumb, regardless of being available or not.
        var largestThumb = asset.FullThumbnailPolicy.SizeList[0];
            
        foreach (var thumbCandidate in thumbsToProcess)
        {
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

            var currentMax = thumb.MaxDimension;
            if (currentMax == largestThumb)
            {
                // The largest thumb always goes to low.jpg as well as the 'normal' place
                var lowKey = StorageKeyGenerator.GetLargestThumbnailLocation(assetId);
                await BucketWriter.WriteFileToBucket(lowKey, thumbCandidate.Path, MIMEHelper.JPEG);
            }

            var thumbKey = StorageKeyGenerator.GetThumbnailLocation(assetId, thumb.MaxDimension, isOpen);
            await BucketWriter.WriteFileToBucket(thumbKey, thumbCandidate.Path, MIMEHelper.JPEG);
        }
            
        await CreateSizesJson(assetId, thumbnailSizes);
    }
}