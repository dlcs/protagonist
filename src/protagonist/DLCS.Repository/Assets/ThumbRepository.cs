using System.Collections.Generic;
using System.Linq;
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
        IBucketReader bucketReader,
        IStorageKeyGenerator storageKeyGenerator, 
        ILogger<ThumbRepository> logger)
    {
        this.logger = logger;
        this.bucketReader = bucketReader;
        this.storageKeyGenerator = storageKeyGenerator;
    }
    
    public async Task<List<int[]>?> GetOpenSizes(AssetId assetId)
    {
        var thumbnailSizes = await GetThumbnailSizes(assetId);
        return thumbnailSizes?.Open;
    }

    public async Task<List<int[]>?> GetAllSizes(AssetId assetId)
    {
        var thumbnailSizes = await GetThumbnailSizes(assetId);

        return thumbnailSizes?.Open
            .Union(thumbnailSizes.Auth)
            .OrderByDescending(wh => wh[0]).ToList();
    }
    
    private async Task<ThumbnailSizes?> GetThumbnailSizes(AssetId assetId)
    {
        var sizesList = storageKeyGenerator.GetThumbsSizesJsonLocation(assetId);

        var thumbnailSizesObject = await bucketReader.GetObjectFromBucket(sizesList);
        var thumbnailSizes = await thumbnailSizesObject.DeserializeFromJson<ThumbnailSizes>();
        if (thumbnailSizes == null)
        {
            logger.LogError("Could not find sizes file for asset '{Asset}'", assetId);
            return thumbnailSizes;
        }

        return thumbnailSizes;
    }
}
