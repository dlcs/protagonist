using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.Core.Streams;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository.Assets;
using IIIF.ImageApi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetVips;
using Thumbs.Settings;
using Size = IIIF.Size;

namespace Thumbs;

public class ThumbnailHandler(
    ILogger<ThumbnailHandler> logger,
    IBucketReader bucketReader,
    IOptions<ThumbsSettings> settings,
    IStorageKeyGenerator storageKeyGenerator,
    IThumbRepository thumbRepository)
{
    /// <summary>
    /// Get <see cref="ThumbnailResponse"/> object, containing actual thumbnail bytes
    /// </summary>
    public async Task<ThumbnailResponse> GetThumbnail(AssetId assetId, ImageRequest imageRequest)
    {
        var sizeCandidate = await GetThumbnailSizeCandidate(assetId, imageRequest);
        
        if (sizeCandidate == null) return ThumbnailResponse.Empty;
        
        if (sizeCandidate.KnownSize)
        {
            var location = storageKeyGenerator.GetThumbnailLocation(assetId, sizeCandidate.LongestEdge!.Value);
            var objectFromBucket = await bucketReader.GetObjectFromBucket(location);
            return ThumbnailResponse.ExactSize(objectFromBucket.Stream);
        }

        if (!settings.Value.Resize)
        {
            logger.LogDebug("Could not find thumbnail for '{Path}' and resizing disabled",
                imageRequest.OriginalPath);
            return ThumbnailResponse.Empty;
        }
        
        var resizableSize = (ResizableSize) sizeCandidate;
        
        // First try larger size
        if (resizableSize.LargerSize != null)
        {
            var downscaled = await ResizeThumbnail(assetId, imageRequest, resizableSize.LargerSize,
                resizableSize.Ideal);
            if (downscaled != null) return ThumbnailResponse.Resized(downscaled);
        }

        // Then try smaller size if allowed
        if (resizableSize.SmallerSize != null && settings.Value.Upscale)
        {
            var resizeThumbnail = await ResizeThumbnail(assetId, imageRequest, resizableSize.SmallerSize,
                resizableSize.Ideal, settings.Value.UpscaleThreshold);
            return ThumbnailResponse.Resized(resizeThumbnail);
        }

        return ThumbnailResponse.Empty;
    }

    private async Task<SizeCandidate?> GetThumbnailSizeCandidate(AssetId assetId, ImageRequest imageRequest)
    {
        var openSizes = await thumbRepository.GetOpenSizes(assetId);
        if (openSizes == null) return null;
        
        var sizes = openSizes.Select(Size.FromArray).ToList();

        var sizeCandidate = ThumbnailCalculator.GetCandidate(sizes, imageRequest, settings.Value.Resize);
        return sizeCandidate;
    }

    private async Task<Stream?> ResizeThumbnail(AssetId assetId, ImageRequest imageRequest,
        Size toResize, Size idealSize, int? maxDifference = 0)
    {
        // if upscaling, verify % difference isn't too great
        if ((maxDifference ?? 0) > 0 && idealSize.MaxDimension > toResize.MaxDimension)
        {
            if (Size.GetSizeIncreasePercent(idealSize, toResize) > maxDifference!.Value)
            {
                logger.LogDebug("The next smallest thumbnail {ToResize} breaks the threshold for '{Path}'",
                    toResize.ToString(), imageRequest.OriginalPath);
                return null;
            }
        }

        // we now have a candidate size - resize that and return
        logger.LogDebug("Resize the {Size} thumbnail for {Path}", toResize.MaxDimension, imageRequest.OriginalPath);

        var largestKey = storageKeyGenerator.GetThumbnailLocation(assetId, toResize.MaxDimension);
        await using var thumbnail = (await bucketReader.GetObjectFromBucket(largestKey)).Stream;

        if (thumbnail.IsNull())
        {
            logger.LogWarning("Thumbnail '{Key}' had no content for resizing", largestKey.Key!);
            return null;
        }

        // libvips reads the source incrementally through a custom source, so the S3 stream is consumed as it resizes
        using var image =
            Image.ThumbnailStream(thumbnail, idealSize.Width, height: idealSize.Height, size: Enums.Size.Force);
        return new MemoryStream(image.JpegsaveBuffer());
    }
}
