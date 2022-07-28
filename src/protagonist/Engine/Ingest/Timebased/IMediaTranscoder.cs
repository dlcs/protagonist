namespace Engine.Ingest.Timebased;

public interface IMediaTranscoder
{
    /// <summary>
    /// Initiate a request to start transcoding asset.
    /// </summary>
    Task<bool> InitiateTranscodeOperation(IngestionContext context, CancellationToken token = default);
}