﻿namespace Engine.Ingest.Timebased.Transcode;

public interface IMediaTranscoder
{
    /// <summary>
    /// Initiate a request to start transcoding asset.
    /// </summary>
    Task<bool> InitiateTranscodeOperation(IngestionContext context, Dictionary<string, string> jobMetadata,
        CancellationToken token = default);
}