using DLCS.AWS.S3.Models;
using DLCS.Core.Types;

namespace DLCS.AWS.S3
{
    public interface IStorageKeyGenerator
    {
        /// <summary>
        /// Get the storage key for specified space/customer/key
        /// </summary>
        /// <param name="customer">Customer Id.</param>
        /// <param name="space">Space id.</param>
        /// <param name="assetKey">Unique Id of the asset.</param>
        /// <returns>/customer/space/imageKey string.</returns>
        string GetStorageKey(int customer, int space, string assetKey);
        
        /// <summary>
        /// Get the storage key for specified asset
        /// </summary>
        /// <param name="assetId">Unique identifier for Asset.</param>
        /// <returns>/customer/space/imageKey string.</returns>
        string GetStorageKey(AssetId assetId);

        /// <summary>
        /// Get <see cref="ObjectInBucket"/> for pre-generated thumbnail
        /// </summary>
        /// <param name="assetId">Unique identifier for Asset</param>
        /// <param name="longestEdge">Size of thumbnail</param>
        /// <param name="open">Whether thumbnail is open or requires auth</param>
        /// <returns><see cref="ObjectInBucket"/> for thumbnail</returns>
        ObjectInBucket GetThumbnailLocation(AssetId assetId, int longestEdge, bool open = true);

        /// <summary>
        /// Get <see cref="ObjectInBucket"/> for legacy pre-generated thumbnail
        /// </summary>
        /// <param name="assetId">Unique identifier for Asset</param>
        /// <param name="width">Width of thumbnail</param>
        /// <param name="height">Height of thumbnail</param>
        /// <returns><see cref="ObjectInBucket"/> for thumbnail</returns>
        ObjectInBucket GetLegacyThumbnailLocation(AssetId assetId, int width, int height);

        /// <summary>
        /// Get <see cref="ObjectInBucket"/> for json object storing pregenerated thumbnail sizes
        /// </summary>
        /// <param name="assetId">Unique identifier for Asset</param>
        /// <returns><see cref="ObjectInBucket"/> for sizes json</returns>
        ObjectInBucket GetThumbsSizesJsonLocation(AssetId assetId);
        
        /// <summary>
        /// Get <see cref="ObjectInBucket"/> for largest pre-generated thumbnail.
        /// i.e. low.jpg
        /// </summary>
        /// <param name="assetId">Unique identifier for Asset</param>
        /// <returns><see cref="ObjectInBucket"/> for largest thumbnail</returns>
        ObjectInBucket GetLargestThumbnailLocation(AssetId assetId);

        /// <summary>
        /// Get <see cref="ObjectInBucket"/> for root location of thumbnails for asset, rather than an individual file
        /// </summary>
        /// <param name="assetId">Unique identifier for Asset</param>
        /// <returns><see cref="ObjectInBucket"/> for largest thumbnail</returns>
        ObjectInBucket GetThumbnailsRoot(AssetId assetId);

        /// <summary>
        /// Get <see cref="ObjectInBucket"/> item in output bucket
        /// </summary>
        /// <param name="key">Object key</param>
        /// <returns><see cref="ObjectInBucket"/> for specified key in output bucket</returns>
        ObjectInBucket GetOutputLocation(string key);
    }
}