namespace DLCS.AWS.ElasticTranscoder.Models;

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

    public TranscodeResult()
    {
    }
    
    public TranscodeResult(TranscodedNotification transcodedNotification)
    {
        Outputs = transcodedNotification.Outputs;
        InputKey = transcodedNotification.Input.Key;
        State = transcodedNotification.State;
        ErrorCode = transcodedNotification.ErrorCode;
    }
}