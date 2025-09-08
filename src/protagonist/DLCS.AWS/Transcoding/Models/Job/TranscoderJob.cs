using DLCS.Core.Collections;
using DLCS.Core.Exceptions;
using DLCS.Core.Types;

namespace DLCS.AWS.Transcoding.Models.Job;

/// <summary>
/// Classes that represent a transcoding job request
/// </summary>
public class TranscoderJob
{
    public string Id { get; init; }

    /// <summary>
    /// DateTime when job was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The code of any error that occurred
    /// </summary>
    public int? ErrorCode { get; set; }
        
    /// <summary>
    /// Details of any errors that occurred during transcoding
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Input for transcoder job
    /// </summary>
    public TranscoderInput Input { get; init; }
    
    /// <summary>
    /// List of transcoder outputs
    /// </summary>
    public IList<TranscoderOutput> Outputs { get; init; }

    /// <summary>
    /// Identifier for queue/pipeline processing the job 
    /// </summary>
    public string PipelineId { get; init; }
    
    /// <summary>
    /// Status of Job - ERROR, COMPLETE, CANCELED, PROGRESSING etc
    /// </summary>
    public string Status { get; init; }
    public TranscoderTiming Timing { get; init; }
    public Dictionary<string, string> UserMetadata { get; init; }
    
    // TODO - should this be shared, off an interface for use with TranscodedNotification too?
    /// <summary>
    /// Get the AssetId for this job from user metadata
    /// </summary>
    public AssetId? GetAssetId()
    {
        try
        {
            return UserMetadata.TryGetValue(TranscodeMetadataKeys.DlcsId, out var rawAssetId)
                ? AssetId.FromString(rawAssetId)
                : null;
        }
        catch (InvalidAssetIdException)
        {
            return null;
        }
    }
    
    /// <summary>
    /// Try get the file size of file of we are storing the origin
    /// </summary>
    /// <returns>Size if found in metadata, else 0</returns>
    public long GetStoredOriginalAssetSize()
    {
        try
        {
            if (UserMetadata.IsNullOrEmpty()) return 0;
            
            return UserMetadata.TryGetValue(TranscodeMetadataKeys.OriginSize, out var originSize)
                ? long.Parse(originSize)
                : 0;
        }
        catch (FormatException)
        {
            return 0;
        }
    }

    public class TranscoderInput
    {
        public string Input { get; init; }
    }

    public class TranscoderOutput
    {
        public string Id { get; init; }

        /// <summary>
        /// Output duration in seconds
        /// </summary>
        public long Duration { get; init; }

        /// <summary>
        /// Output duration in ms
        /// </summary>
        public long DurationMillis { get; init; }

        /// <summary>
        /// Height of transcode in px
        /// </summary>
        public int? Height { get; init; }

        /// <summary>
        /// Width of transcode in px
        /// </summary>
        public int? Width { get; init; }

        /// <summary>
        /// The interim output key where this output was transcoded to. This is the interim location where the tool
        /// transcoded the job to and may not be the final location.
        /// </summary>
        public string TranscodeKey { get; init; }

        /// <summary>
        /// The key where the DLCS will store the transcode for this output. Only populated if the job is Complete.
        /// </summary>
        public string? Key { get; init; }
        
        /// <summary>
        /// The extension used for this output
        /// </summary>
        public string Extension { get; init; }
        
        /// <summary>
        /// Preset name used for this output
        /// </summary>
        public string PresetId { get; init; }
    }

    public class TranscoderTiming
    {
        public long FinishTimeMillis { get; init; }
        public long StartTimeMillis { get; init; }
        public long SubmitTimeMillis { get; init; }
    }

    public bool IsComplete() => string.Equals(Status, "COMPLETE", StringComparison.OrdinalIgnoreCase);
}
