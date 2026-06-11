namespace DLCS.Model.Assets;

public enum BatchStatus
{
    /// <summary>
    /// Placeholder
    /// </summary>
    Unknown = 0,
    
    /// <summary>
    /// Asset is waiting to be picked up or is in-flight
    /// </summary>
    Waiting = 1,
    
    /// <summary>
    /// Asset failed to ingest
    /// </summary>
    Error = 2,
    
    /// <summary>
    /// Asset completed successfully
    /// </summary>
    Completed = 3,
}
