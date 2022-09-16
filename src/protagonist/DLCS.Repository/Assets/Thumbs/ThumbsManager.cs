using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using IIIF;
using Newtonsoft.Json;

namespace DLCS.Repository.Assets.Thumbs;

/// <summary>
/// Base class for classes that handle creating/moving thumbnails
/// </summary>
public abstract class ThumbsManager
{
    protected readonly IBucketWriter BucketWriter;
    protected readonly IStorageKeyGenerator StorageKeyGenerator;

    public ThumbsManager(
        IBucketWriter bucketWriter,
        IStorageKeyGenerator storageKeyGenerator
    )
    {
        BucketWriter = bucketWriter;
        StorageKeyGenerator = storageKeyGenerator;
    }
    protected static Size GetMaxAvailableThumb(Asset asset, ThumbnailPolicy policy)
    {
        var _ = asset.GetAvailableThumbSizes(policy, out var maxDimensions);
        return Size.Square(maxDimensions.maxBoundedSize);
    }

    protected async Task CreateSizesJson(AssetId assetId, ThumbnailSizes thumbnailSizes)
    {
        var sizesDest = StorageKeyGenerator.GetThumbsSizesJsonLocation(assetId);
        await BucketWriter.WriteToBucket(sizesDest, JsonConvert.SerializeObject(thumbnailSizes),
            "application/json");
    }
}