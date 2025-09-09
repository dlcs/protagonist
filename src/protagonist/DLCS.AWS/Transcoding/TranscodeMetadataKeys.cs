using DLCS.Core.Collections;
using DLCS.Core.Exceptions;
using DLCS.Core.Types;

namespace DLCS.AWS.Transcoding;

/// <summary>
/// Constant values used for transcoding job metadata
/// </summary>
public static class TranscodeMetadataKeys
{
    /// <summary>
    /// Key for unique Id in the DLCS of the asset being transcoded.
    /// </summary>
    public const string DlcsId = "dlcsId";
        
    /// <summary>
    /// Key for StartTime when request was made.
    /// </summary>
    public const string StartTime = "startTime";
        
    /// <summary>
    /// A random Id associated with Job.
    /// </summary>
    public const string JobId = "jobId";

    /// <summary>
    /// Key for the size of origin file saved in DLCS (may be 0)
    /// </summary>
    public const string OriginSize = "storedOriginSize";

    /// <summary>
    /// Key for the BatchId this asset is part of
    /// </summary>
    public const string BatchId = "batchId";
    
    /// <summary>
    /// MediaType of the asset transcode job is for
    /// </summary>
    public const string MediaType = "mediaType";
    
    /// <summary>
    /// Get the AssetId for this job from user metadata
    /// </summary>
    public static AssetId? GetAssetId(this ITranscoderJobMetadata job)
    {
        try
        {
            return job.UserMetadata.TryGetValue(DlcsId, out var rawAssetId)
                ? AssetId.FromString(rawAssetId)
                : null;
        }
        catch (InvalidAssetIdException)
        {
            return null;
        }
    }
    
    /// <summary>
    /// Get the BatchId, if found, for this job from user metadata
    /// </summary>
    public static int? GetBatchId(this ITranscoderJobMetadata job)
    {
        if (!job.UserMetadata.TryGetValue(BatchId, out var rawBatchId)) return null;

        return int.TryParse(rawBatchId, out var batchId) ? batchId : null;
    }
    
    /// <summary>
    /// Try get the file size of file of we are storing the origin
    /// </summary>
    /// <returns>Size if found in metadata, else 0</returns>
    public static long GetStoredOriginalAssetSize(this ITranscoderJobMetadata job)
    {
        try
        {
            return job.UserMetadata.TryGetValue(OriginSize, out var originSize)
                ? long.Parse(originSize)
                : 0;
        }
        catch (FormatException)
        {
            return 0;
        }
    }
}

/// <summary>
/// Marker interface for classes that have transcoder job metadata
/// </summary>
public interface ITranscoderJobMetadata
{
    Dictionary<string, string> UserMetadata { get; }
}
