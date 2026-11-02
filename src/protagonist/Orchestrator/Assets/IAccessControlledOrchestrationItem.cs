using System.Collections.Generic;
using DLCS.Core.Types;

namespace Orchestrator.Assets;

/// <summary>
/// Implemented by orchestration items (assets, adjuncts) that can be the subject of access control
/// </summary>
public interface IAccessControlledOrchestrationItem
{
    /// <summary>
    /// Get boolean indicating whether item is restricted or not
    /// </summary>
    bool RequiresAuth { get; }

    /// <summary>
    /// Gets list of roles associated with item
    /// </summary>
    IReadOnlyList<string> Roles { get; }
    
    /// <summary>
    /// Identifier for this deliverable
    /// </summary>
    DeliverableId DeliverableId { get; }
}
