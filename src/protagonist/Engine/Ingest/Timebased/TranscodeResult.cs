using Engine.Ingest.Handlers;

namespace Engine.Ingest.Timebased;
    
/// <summary>
/// Represents the overall result of a transcode operation.
/// </summary>
public class TranscodeResult
{
    /// <summary>
    /// The Key of the 'input' file that served as source of transcoding.
    /// </summary>
    public string? InputKey { get; }
        
    /// <summary>
    /// The results of transcode operations.
    /// </summary>
    public IList<TranscodeOutput> Outputs { get; }
    
    /// <summary>
    /// PROGRESSING|COMPLETED|WARNING|ERROR
    /// </summary>
    public string State { get; }
    
    /// <summary>
    /// Details of any error that may have occurred
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Check if State is "COMPLETED"
    /// </summary>
    public bool IsComplete() => string.Equals(State, "COMPLETED", StringComparison.OrdinalIgnoreCase);

    public TranscodeResult(ElasticTranscoderMessage elasticTranscoderMessage)
    {
        Outputs = elasticTranscoderMessage.Outputs;
        InputKey = elasticTranscoderMessage.Input.Key;
        State = elasticTranscoderMessage.State;
        ErrorCode = elasticTranscoderMessage.ErrorCode;
    }
}

/// <summary>
/// Represents 'Output' element of job transcode message
/// </summary>
public class TranscodeOutput
{
    public string Id { get; set; }
    public string PresetId { get; set; }
    public string Key { get; set; }
    
    /// <summary>
    /// Status of output (Progressing|Complete|Warning|Error)
    /// </summary>
    public string Status { get; set; }
    public long Duration { get; set; }
    public long? DurationMillis { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    
    /// <summary>
    /// Check if Status is "Complete"
    /// </summary>
    public bool IsComplete() => string.Equals(Status, "Complete", StringComparison.OrdinalIgnoreCase);

    public long GetDuration() => DurationMillis ?? Duration * 1000;
}
