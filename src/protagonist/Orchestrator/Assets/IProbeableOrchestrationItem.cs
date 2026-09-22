using System.Collections.Generic;

namespace Orchestrator.Assets;

/// <summary>
/// Implemented by orchestration items (assets, adjuncts) that can be the subject of an auth probe service request
/// </summary>
public interface IProbeableOrchestrationItem
{
    /// <summary>
    /// Get boolean indicating whether item is restricted or not
    /// </summary>
    bool RequiresAuth { get; }

    /// <summary>
    /// Gets list of roles associated with item
    /// </summary>
    List<string> Roles { get; }
}
