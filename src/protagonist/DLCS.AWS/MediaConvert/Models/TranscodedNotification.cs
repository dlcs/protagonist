using DLCS.AWS.Transcoding;
using DLCS.Core.Exceptions;
using DLCS.Core.Types;

namespace DLCS.AWS.MediaConvert.Models;

/// <summary>
/// The body of a notification sent out from MediaConvert
/// </summary>
/// <remarks>
/// https://docs.aws.amazon.com/mediaconvert/latest/ug/ev_status_error.html
/// https://docs.aws.amazon.com/mediaconvert/latest/ug/ev_status_complete.html
/// </remarks>
public class TranscodedNotification
{
    /// <summary>
    /// Api version used to create job
    /// </summary>
    public string Version { get; set; }
    
    /// <summary>
    /// Unique identifier for this notification (this is not the MediaConvert job id - see details)
    /// </summary>
    public string Id { get; set; }
    
    /// <summary>
    /// When this job was raised
    /// </summary>
    public DateTime Time { get; set; }
    
    /// <summary>
    /// Value of PipelineId in Create Job Request
    /// </summary>
    public TranscodeNotificationDetail Detail { get; set; }
    
    /// <summary>
    /// Get the AssetId for this job from user metadata
    /// </summary>
    public AssetId? GetAssetId()
    {
        try
        {
            return Detail.UserMetadata.TryGetValue(TranscodeMetadataKeys.DlcsId, out var rawAssetId)
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
    public int? GetBatchId()
    {
        if (!Detail.UserMetadata.TryGetValue(TranscodeMetadataKeys.BatchId, out var rawBatchId)) return null;

        return int.TryParse(rawBatchId, out var batchId) ? batchId : null;
    }
    
    public class TranscodeNotificationDetail
    {
        /// <summary>
        /// Message timestamp
        /// </summary>
        public string Timestamp { get; set; }
        
        /// <summary>
        /// MediaConvert JobId
        /// </summary>
        public string JobId { get; set; }
        
        /// <summary>
        /// Queue this notification is for
        /// </summary>
        public string Queue { get; set; }
        
        /// <summary>
        /// Overall status of job (ERROR or COMPLETE)
        /// </summary>
        public string Status { get; set; }
        
        /// <summary>
        /// The code of any error that occurred
        /// </summary>
        public int? ErrorCode { get; set; }
        
        /// <summary>
        /// Details of any errors that occurred during transcoding
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// User provided metadata associated with this job
        /// </summary>
        public Dictionary<string, string> UserMetadata { get; set; }
    }
}
