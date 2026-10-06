using System.Collections.Generic;
using DLCS.Model.Assets;

namespace Orchestrator.Assets;

public static class AdjunctX
{
    /// <summary>
    /// Get the roles that apply when delivering this adjunct.
    /// </summary>
    /// <remarks>Adjuncts don't currently have their own roles, they inherit those of the parent Asset</remarks>
    public static IReadOnlyList<string> GetDeliverableRoles(this Adjunct adjunct)
        => adjunct.Asset?.Roles ?? [];
}
