namespace Engine.Messaging;

/// <summary>
/// Enum representing the different types of queue.
/// </summary>
public enum EngineMessageType
{
    /// <summary>
    /// Queue used for ingesting assets.
    /// </summary>
    Ingest = 0,
        
    /// <summary>
    /// Queue for responding to Transcode completion events
    /// </summary>
    TranscodeComplete = 1
}