using System.Collections.Generic;
using DLCS.Core.Types;

namespace Orchestrator.Assets;

/// <summary>
/// Lightweight <see cref="IAccessControlledOrchestrationItem"/> for validating access to a set of roles where there is
/// no backing asset or adjunct.
/// </summary>
/// <param name="DeliverableId">Identifier the roles are being validated for</param>
/// <param name="Roles">Roles to validate</param>
public record AccessControlledItem(DeliverableId DeliverableId, IReadOnlyList<string> Roles)
    : IAccessControlledOrchestrationItem
{
    /// <inheritdoc/>
    public bool RequiresAuth => Roles.Count > 0;
}
