using DLCS.Core.Types;
using DLCS.Model.Assets;
using thumbConsts =  DLCS.Repository.Settings.ThumbsSettings.Constants;

namespace DLCS.Repository.Storage
{
    public static class StorageKeyGenerator
    {
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
        /// Get path for s.json file. ({key}/s.json) 
        /// </summary>
        public static string GetSizesJsonPath(string key)
            => string.Concat(key, thumbConsts.SizesJsonKey);
    }
}