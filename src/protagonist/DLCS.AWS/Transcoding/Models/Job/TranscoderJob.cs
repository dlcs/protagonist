namespace DLCS.AWS.Transcoding.Models.Job;

/// <summary>
/// Classes that represent a transcoding job request. This has been normalised from payload in transcoding service
/// </summary>
/// <remarks>
/// The specific fields here will be dependant on the downstream system but is an internal normalised form of job.
/// </remarks>
public class TranscoderJob : ITranscoderJobMetadata
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
    
    /// <summary>
    /// Details of job timing
    /// </summary>
    public TranscoderTiming Timing { get; init; }
    
    /// <summary>
    /// Collection of custom metadata added to job and echoed back 
    /// </summary>
    public Dictionary<string, string> UserMetadata { get; init; } = new();
    
    public class TranscoderInput
    {
        /// <summary>
        /// Location of input file
        /// </summary>
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
        /// <summary>
        /// Unix timestamp when job finished
        /// </summary>
        public long? FinishTimeMillis { get; init; }
        
        /// <summary>
        /// Unix timestamp when job started
        /// </summary>
        public long? StartTimeMillis { get; init; }
        
        /// <summary>
        /// Unix timestamp when job was submitted
        /// </summary>
        public long SubmitTimeMillis { get; init; }
    }

    public bool IsComplete() => string.Equals(Status, "COMPLETE", StringComparison.OrdinalIgnoreCase);
}
