using API.Features.Queues.Requests;
using DLCS.HydraModel;

namespace API.Features.Queues.Converters;

/// <summary>
/// Convert to queue summary hydra model
/// </summary>
public static class QueueSummaryConverter
{
    /// <summary>
    /// Convert queue count model to API resource 
    /// </summary>
    public static QueueSummary ToHydra(this QueueCount queueCounts, string baseUrl)
    {
        var hydra = new QueueSummary(baseUrl);
        hydra.Incoming = queueCounts.Incoming;
        hydra.Priority = queueCounts.Priority;
        hydra.Timebased = queueCounts.Timebased;
        hydra.File = queueCounts.File;
        hydra.TranscodeComplete = queueCounts.TranscodeComplete;

        return hydra;
    }
}