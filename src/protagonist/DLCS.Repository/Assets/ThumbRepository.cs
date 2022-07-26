using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Assets;

public class ThumbRepository : IThumbRepository
{
    private readonly ILogger<ThumbRepository> logger;
    private readonly IBucketReader bucketReader;
    private readonly IStorageKeyGenerator storageKeyGenerator;

    public ThumbRepository(
        ILogger<ThumbRepository> logger,
        IBucketReader bucketReader,
        IStorageKeyGenerator storageKeyGenerator)
    {
        this.logger = logger;
        this.bucketReader = bucketReader;
        this.storageKeyGenerator = storageKeyGenerator;
    }
    
    public async Task<List<int[]>?> GetOpenSizes(AssetId assetId)
    {
        ObjectInBucket sizesList = storageKeyGenerator.GetThumbsSizesJsonLocation(assetId);

        var thumbnailSizesObject = await bucketReader.GetObjectFromBucket(sizesList);
        var thumbnailSizes = await thumbnailSizesObject.DeserializeFromJson<ThumbnailSizes>();
        if (thumbnailSizes == null)
        {
            logger.LogError("Could not find sizes file for asset '{Asset}'", assetId);
            return null;
        }
        return thumbnailSizes.Open;
    }
}
