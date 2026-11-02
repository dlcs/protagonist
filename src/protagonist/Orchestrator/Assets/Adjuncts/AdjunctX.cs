using System;
using System.Collections.Generic;
using DLCS.Model.Assets;

namespace Orchestrator.Assets;

public static class AdjunctX
{
    /// <summary>
    /// Get the roles that apply when delivering this adjunct.
    /// </summary>
    /// <remarks>Adjuncts don't currently have their own roles, they inherit those of the parent Asset</remarks>
    /// <exception cref="InvalidOperationException">Thrown if the parent Asset has not been loaded</exception>
    public static IReadOnlyList<string> GetDeliverableRoles(this Adjunct adjunct)
    {
        // Treating a missing Asset as "no roles" would make a restricted adjunct open, so fail instead
        if (adjunct.Asset is null)
        {
            throw new InvalidOperationException(
                $"Unable to get roles for {adjunct.Identifier()} as parent Asset has not been loaded");
        }

        return adjunct.Asset.Roles ?? [];
    }
}
