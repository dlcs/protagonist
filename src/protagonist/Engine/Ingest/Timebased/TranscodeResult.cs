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

    public TranscodeResult(string? inputKey = null, IList<TranscodeOutput>? outputs = null)
    {
        InputKey = inputKey;
        Outputs = outputs ?? new List<TranscodeOutput>();
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

    public long GetDuration() => DurationMillis ?? Duration * 1000;
}
