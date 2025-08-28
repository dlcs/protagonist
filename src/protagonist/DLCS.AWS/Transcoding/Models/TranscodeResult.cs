using DLCS.Core.Collections;

namespace DLCS.AWS.Transcoding.Models;

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
    public int? ErrorCode { get; }

    /// <summary>
    /// Check if State is "COMPLETED"
    /// </summary>
    public bool IsComplete() => string.Equals(State, "COMPLETED", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Any UserMetadata associated with the request. Set when creating job and echoed back.
    /// </summary>
    public Dictionary<string, string> UserMetadata { get; }

    public TranscodeResult()
    {
    }
    
    public TranscodeResult(TranscodedNotification transcodedNotification)
    {
        Outputs = transcodedNotification.Outputs;
        InputKey = transcodedNotification.Input.Key;
        State = transcodedNotification.State;
        ErrorCode = transcodedNotification.ErrorCode;
        UserMetadata = transcodedNotification.UserMetadata;
    }
    
    /// <summary>
    /// Get the AssetId for this job from user metadata
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
}
