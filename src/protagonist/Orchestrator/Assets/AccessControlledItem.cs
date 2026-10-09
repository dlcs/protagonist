using System.Collections.Generic;
using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace Orchestrator.Assets;

/// <summary>
/// Lightweight, generic <see cref="IAccessControlledOrchestrationItem"/> that can be either an <see cref="Adjunct"/>
/// or <see cref="Asset"/>
/// </summary>
/// <param name="DeliverableId">Identifier the roles are being validated for</param>
/// <param name="Roles">Roles to validate</param>
public record AccessControlledItem(DeliverableId DeliverableId, IReadOnlyList<string> Roles)
    : IAccessControlledOrchestrationItem
{
    /// <inheritdoc/>
    public bool RequiresAuth => Roles.Count > 0;
}
