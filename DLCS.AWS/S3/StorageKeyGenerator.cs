using System;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.S3
{
    [Obsolete("Use S3BucketKeyGenerator")]
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
    
    // Put all the things that generate keys into this - then inject it
    // this is the only way to get a key for s3?
    public class S3BucketKeyGenerator : IBucketKeyGenerator
    {
        private readonly S3Settings s3Options;

        public S3BucketKeyGenerator(IOptions<S3Settings> s3Options)
        {
            this.s3Options = s3Options.Value;
        }
        
        /// <summary>
        /// Key of the json file that contains available sizes
        /// </summary>
        public const string SizesJsonKey = "s.json";
        
        /// <summary>
        /// Key of the largest pre-generated thumbnail
        /// </summary>
        public const string LargestThumbKey = "low.jpg";
        
        /// <summary>
        /// S3 slug where open thumbnails are stored.
        /// </summary>
        public const string OpenSlug = "open";

        /// <summary>
        /// S3 slug where thumbnails requiring authorisation are stored.
        /// </summary>
        public const string AuthorisedSlug = "auth";

        /// <summary>
        /// Get the storage key for specified space/customer/key
        /// </summary>
        /// <param name="customer">Customer Id.</param>
        /// <param name="space">Space id.</param>
        /// <param name="assetKey">Unique Id of the asset.</param>
        /// <returns>/customer/space/imageKey string.</returns>
        public string GetStorageKey(int customer, int space, string assetKey)
            => $"{customer}/{space}/{assetKey}";

        /// <summary>
        /// Get the storage key for specified asset
        /// </summary>
        /// <param name="assetId">Unique identifier for Asset.</param>
        /// <returns>/customer/space/imageKey string.</returns>
        public string GetStorageKey(AssetId assetId)
            => GetStorageKey(assetId.Customer, assetId.Space, assetId.Asset);
        
        public ObjectInBucket GetThumbnailLocation(AssetId assetId, int longestEdge, bool open = true)
        {
            var accessPrefix = open ? OpenSlug : AuthorisedSlug;
            var key = $"{GetStorageKey(assetId)}/{accessPrefix}/{longestEdge}.jpg";
            return new ObjectInBucket(s3Options.ThumbsBucket, key);
        }

        public ObjectInBucket GetLegacyThumbnailLocation(AssetId assetId, int width, int height)
        {
            var key = $"{GetStorageKey(assetId)}/full/{width},{height}/0/default.jpg";
            return new ObjectInBucket(s3Options.ThumbsBucket, key);
        }

        public ObjectInBucket GetThumbsSizesJsonLocation(AssetId assetId)
        {
            var key = $"{GetStorageKey(assetId)}/{SizesJsonKey}";
            return new ObjectInBucket(s3Options.ThumbsBucket, key);
        }

        public ObjectInBucket GetLargestThumbnailLocation(AssetId assetId)
        {
            var key = $"{GetStorageKey(assetId)}/{LargestThumbKey}";
            return new ObjectInBucket(s3Options.ThumbsBucket, key);
        }

        public ObjectInBucket GetThumbnailsRoot(AssetId assetId)
        {
            var key = $"{GetStorageKey(assetId)}/";
            return new ObjectInBucket(s3Options.ThumbsBucket, key);
        }

        public ObjectInBucket GetOutputLocation(string key)
            => new(s3Options.OutputBucket, key);
    }

    public interface IBucketKeyGenerator
    {
        string GetStorageKey(int customer, int space, string assetKey);
        
        string GetStorageKey(AssetId assetId);

        ObjectInBucket GetThumbnailLocation(AssetId assetId, int longestEdge, bool open = true);

        ObjectInBucket GetLegacyThumbnailLocation(AssetId assetId, int width, int height);

        ObjectInBucket GetThumbsSizesJsonLocation(AssetId assetId);
        
        ObjectInBucket GetLargestThumbnailLocation(AssetId assetId);

        ObjectInBucket GetThumbnailsRoot(AssetId assetId);

        ObjectInBucket GetOutputLocation(string key);
    }
}