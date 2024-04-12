using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
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
    protected readonly IAssetApplicationMetadataRepository AssetApplicationMetadataRepository;

    public ThumbsManager(
        IBucketWriter bucketWriter,
        IStorageKeyGenerator storageKeyGenerator,
        IAssetApplicationMetadataRepository assetApplicationMetadataRepository
    )
    {
        BucketWriter = bucketWriter;
        StorageKeyGenerator = storageKeyGenerator;
        AssetApplicationMetadataRepository = assetApplicationMetadataRepository;
    }
    
    protected static Size GetMaxAvailableThumb(Asset asset, ThumbnailPolicy policy)
    {
        var _ = asset.GetAvailableThumbSizes(policy, out var maxDimensions);
        return Size.Square(maxDimensions.maxBoundedSize);
    }

    protected async Task CreateSizesJson(AssetId assetId, ThumbnailSizes thumbnailSizes)
    {
        var serializedThumbnailSizes = JsonConvert.SerializeObject(thumbnailSizes);
        var sizesDest = StorageKeyGenerator.GetThumbsSizesJsonLocation(assetId);
        await BucketWriter.WriteToBucket(sizesDest, serializedThumbnailSizes,
            "application/json");
        await AssetApplicationMetadataRepository.UpsertApplicationMetadata(assetId,
            AssetApplicationMetadataTypes.ThumbnailPolicy, serializedThumbnailSizes);
    }
}