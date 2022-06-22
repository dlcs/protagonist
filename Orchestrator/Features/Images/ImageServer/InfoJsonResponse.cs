using IIIF;

namespace Orchestrator.Features.Images.ImageServer;

/// <summary>
/// Internal model for tracking info json generation
/// </summary>
public class InfoJsonResponse
{
    /// <summary>
    /// Generated info json object
    /// </summary>
    public JsonLdBase InfoJson { get; }

    /// <summary>
    /// Whether orchestration was attempted as part of this request. This doesn't necessarily mean orchestration
    /// happened, purely that it was attempted. The image may have been orchestrated already, in which case it was a
    /// no-op
    /// </summary>
    public bool WasOrchestrated { get; }

    public InfoJsonResponse(JsonLdBase infoJson, bool wasOrchestrated)
    {
        InfoJson = infoJson;
        WasOrchestrated = wasOrchestrated;
    }
}