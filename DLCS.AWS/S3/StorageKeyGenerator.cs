using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace DLCS.AWS.S3
{
    public static class StorageKeyGenerator
    {
        /// <summary>
        /// Key of the json file that contains available sizes
        /// </summary>
        public const string SizesJsonKey = "s.json";
        
        /// <summary>
        /// Key of the largest pre-generated thumbnail
        /// </summary>
        public const string LargestThumbKey = "low.jpg";
        
        /// <summary>
        /// Get the storage key for specified space/customer/key
        /// </summary>
        /// <param name="customer">Customer Id.</param>
        /// <param name="space">Space id.</param>
        /// <param name="assetKey">Unique Id of the asset.</param>
        /// <returns>/customer/space/imageKey string.</returns>
        public static string GetStorageKey(int customer, int space, string assetKey)
            => $"{customer}/{space}/{assetKey}";

        /// <summary>
        /// Get the storage key for specified asset
        /// </summary>
        /// <param name="assetId">Unique identifier for Asset.</param>
        /// <returns>/customer/space/imageKey string.</returns>
        public static string GetStorageKey(AssetId assetId)
            => GetStorageKey(assetId.Customer, assetId.Space, assetId.Asset);
            
        /// <summary>
        /// Get the storage get for specified space/customer/key
        /// </summary>
        /// <param name="asset">Asset to get storage key for.</param>
        /// <returns>/customer/space/imageKey string.</returns>
        public static string GetStorageKey(this Asset asset)
            => GetStorageKey(asset.Customer, asset.Space, asset.GetUniqueName());
        
        /// <summary>
        /// Get path for s.json file. ({assetKey}/s.json) 
        /// </summary>
        public static string GetSizesJsonPath(string assetKey)
            => string.Concat(assetKey, SizesJsonKey);

        /// <summary>
        /// Get path for low.jpg file. ({assetKey}/low.jpg) 
        /// </summary>
        public static string GetLargestThumbPath(string assetKey)
            => assetKey[^1] == '/'
                ? string.Concat(assetKey, LargestThumbKey)
                : string.Concat(assetKey, "/", LargestThumbKey);
    }
}