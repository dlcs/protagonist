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
public class S3StorageKeyGenerator(IOptions<AWSSettings> awsOptions) : IStorageKeyGenerator
{
    private readonly S3Settings s3Options = awsOptions.Value.S3;
    private readonly AWSSettings awsSettings = awsOptions.Value;
    private static readonly Random Random = new();

    /// <summary>
    /// Key of the json file that contains available sizes
    /// </summary>
    public const string SizesJsonKey = "s.json";
    
    /// <summary>
    /// Key of the file that contains metadata about asset
    /// </summary>
    public const string MetadataKey = "metadata";
    
    /// <summary>
    /// S3 slug where open thumbnails are stored.
    /// </summary>
    public const string OpenSlug = "open";

    /// <summary>
    /// S3 slug where thumbnails requiring authorisation are stored.
    /// </summary>
    public const string AuthorisedSlug = "auth";

    /// <summary>
    /// Get the storage key for specified asset
    /// </summary>
    /// <param name="assetId">Unique identifier for Asset.</param>
    /// <returns>/customer/space/imageKey string.</returns>
    public static string GetStorageKey(AssetId assetId) => assetId.ToString();

    public RegionalisedObjectInBucket GetStorageLocation(AssetId assetId)
    {
        var key = GetStorageKey(assetId);
        return new RegionalisedObjectInBucket(s3Options.StorageBucket, key, awsSettings.Region);
    }
    
    public ObjectInBucket GetStorageLocationRoot(AssetId assetId)
    {
        var key = GetStorageKey(assetId);
        return new ObjectInBucket(s3Options.StorageBucket, $"{key}/");
    }

    public RegionalisedObjectInBucket GetStoredOriginalLocation(AssetId assetId)
    {
        var key = $"{GetStorageKey(assetId)}/original";
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

    public ObjectInBucket GetInfoJsonRoot(AssetId assetId)
    {
        var key = $"{GetStorageKey(assetId)}/info/";
        return new ObjectInBucket(s3Options.StorageBucket, key);
    }

    public ObjectInBucket GetInfoJsonLocation(AssetId assetId, string imageServer, IIIF.ImageApi.Version imageApiVersion)
    {
        var versionSlug = imageApiVersion == IIIF.ImageApi.Version.V2 ? "v2" : "v3";
        var key = $"{GetStorageKey(assetId)}/info/{imageServer}/{versionSlug}/info.json";
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

    public ObjectInBucket? TryParseTimebasedInputLocation(string inputLocation)
    {
        var parsed = RegionalisedObjectInBucket.Parse(inputLocation);
        return parsed != null && parsed.Bucket == s3Options.TimebasedInputBucket ? parsed : null;
    }

    public ObjectInBucket GetTimebasedOutputLocation(string key)
        => new(s3Options.TimebasedOutputBucket, key);

    public ObjectInBucket GetTimebasedMetadataLocation(AssetId assetId)
    {
        var key = $"{GetStorageKey(assetId)}/{MetadataKey}";
        return new ObjectInBucket(s3Options.StorageBucket, key);
    }

    public ObjectInBucket GetOriginRoot(AssetId assetId)
    {
        var key = GetStorageKey(assetId);
        return new ObjectInBucket(s3Options.OriginBucket, $"{key}/");
    }
    
    public ObjectInBucket GetOriginStrategyCredentialsLocation(int customerId, string originStrategyId)
    {
        var key = $"{customerId}/origin-strategy/{originStrategyId}/credentials.json";
        return new ObjectInBucket(s3Options.SecurityObjectsBucket, key);
    }

    public RegionalisedObjectInBucket GetTransientImageLocation(AssetId assetId)
    {
        var key = GetStorageKey(assetId);
        return new RegionalisedObjectInBucket(s3Options.StorageBucket, $"transient/{key}", awsSettings.Region);
    }

    public ObjectInBucket GetTranscodeDestinationRoot(AssetId assetId, string jobId)
    {
        var key = $"{jobId}/{GetStorageKey(assetId)}/transcode";
        return new ObjectInBucket(s3Options.TimebasedOutputBucket, key);
    }
}
