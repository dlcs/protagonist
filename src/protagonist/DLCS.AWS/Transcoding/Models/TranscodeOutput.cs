namespace DLCS.AWS.Transcoding.Models;

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
    
    public string StatusDetail { get; set; }
    public long Duration { get; set; }
    public long? DurationMillis { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    
    /// <summary>
    /// Check if Status is "Complete"
    /// </summary>
    public bool IsComplete() => string.Equals(Status, "Complete", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the duration of transcode in milliseconds
    /// </summary>
    public long GetDuration() => DurationMillis ?? Duration * 1000;
}
