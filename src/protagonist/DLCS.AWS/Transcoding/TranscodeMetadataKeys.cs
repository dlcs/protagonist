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
}
