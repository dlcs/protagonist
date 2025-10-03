namespace Engine.Ingest;

/// <summary>
/// Represents the result of an ingest request.
/// </summary>
public enum IngestResultStatus
{
    /// <summary>
    /// Fallback value.
    /// </summary>
    Unknown = 0,
        
    /// <summary>
    /// Ingestion completed successfully.
    /// </summary>
    Success = 1,
        
    /// <summary>
    /// Ingestion operation failed.
    /// </summary>
    Failed = 2,
        
    /// <summary>
    /// Ingestion operation has successfully been queued for further processing (e.g. by MediaConvert)
    /// </summary>
    QueuedForProcessing = 3,
    
    /// <summary>
    /// Ingestion operation failed because it would exceed customers storage policy limits
    /// </summary>
    StorageLimitExceeded = 4
}
