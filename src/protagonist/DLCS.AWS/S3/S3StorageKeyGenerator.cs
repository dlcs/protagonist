using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.S3;

/// <summary>
/// Contains methods for getting ObjectInBucket objects for specific buckets/items 
/// </summary>
/// <remarks>
/// This should be the only class that knows/cares about buckets - calling code should only deal with
/// the ObjectInBucket objects for accessing s3 objects
/// </remarks>
public class S3StorageKeyGenerator : IStorageKeyGenerator
{
    private readonly S3Settings s3Options;
    private readonly AWSSettings awsSettings;
    private static readonly Random Random = new();

    public S3StorageKeyGenerator(IOptions<AWSSettings> awsOptions)
    {
        awsSettings = awsOptions.Value;
        s3Options = awsOptions.Value.S3;
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
    public static string GetStorageKey(int customer, int space, string assetKey)
        => $"{customer}/{space}/{assetKey}";

    /// <summary>
    /// Get the storage key for specified asset
    /// </summary>
    /// <param name="assetId">Unique identifier for Asset.</param>
    /// <returns>/customer/space/imageKey string.</returns>
    public static string GetStorageKey(AssetId assetId)
        => GetStorageKey(assetId.Customer, assetId.Space, assetId.Asset);

    public RegionalisedObjectInBucket GetStorageLocation(AssetId assetId)
    {
        var key = GetStorageKey(assetId);
        return new RegionalisedObjectInBucket(s3Options.StorageBucket, key, awsSettings.Region);
    }

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

    public RegionalisedObjectInBucket GetTimebasedAssetLocation(AssetId assetId, string assetPath)
    {
        var fullPath = GetStorageKey(assetId).ToConcatenated('/', assetPath);
        return new RegionalisedObjectInBucket(s3Options.StorageBucket, fullPath, awsSettings.Region);
    }

    public ObjectInBucket GetTimebasedAssetLocation(string fullAssetPath)
        => new(s3Options.StorageBucket, fullAssetPath);

    public ObjectInBucket GetInfoJsonLocation(AssetId assetId, string imageServer, IIIF.ImageApi.Version imageApiVersion)
    {
        var versionSlug = imageApiVersion == IIIF.ImageApi.Version.V2 ? "v2" : "v3";
        var key = $"info/{imageServer}/{versionSlug}/{GetStorageKey(assetId)}/info.json";
        return new ObjectInBucket(s3Options.StorageBucket, key);
    }

    public RegionalisedObjectInBucket GetAssetAtOriginLocation(AssetId assetId)
    {
        var fullPath = GetStorageKey(assetId);
        return new RegionalisedObjectInBucket(s3Options.OriginBucket, fullPath, awsSettings.Region);
    }

    public void EnsureRegionSet(RegionalisedObjectInBucket objectInBucket)
    {
        if (objectInBucket.Region.HasText()) return;

        objectInBucket.Region = awsSettings.Region;
    }

    public Uri GetS3Uri(ObjectInBucket objectInBucket, bool useRegion = false)
        => useRegion
            ? objectInBucket.GetLegacyS3Uri(awsSettings.Region)
            : objectInBucket.GetS3Uri();


    public ObjectInBucket GetTimebasedInputLocation(AssetId assetId)
    {
        var postfix = Random.Next(0, 9999).ToString("D4");
        var fullPath = GetStorageKey(assetId).ToConcatenated('/', postfix);
        return new ObjectInBucket(s3Options.TimebasedInputBucket, fullPath);
    }

    public ObjectInBucket GetTimebasedInputLocation(string key)
        => new(s3Options.TimebasedInputBucket, key);

    public ObjectInBucket GetTimebasedOutputLocation(string key)
        => new(s3Options.TimebasedOutputBucket, key);
}