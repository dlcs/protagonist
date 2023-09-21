using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;

namespace Orchestrator.Infrastructure.Auth;

public interface IAssetAccessValidator
{
    /// <summary>
    /// Validate whether current request has access to the specified roles for customer.
    /// This will try to validate request using specified <see cref="AuthMechanism"/>
    /// </summary>
    /// <param name="assetId">Current assetId</param>
    /// <param name="roles">Roles associated with Asset</param>
    /// <param name="mechanism">Which mechanism to use to authorize user</param>
    /// <returns><see cref="AssetAccessResult"/> enum representing result of validation</returns>
    Task<AssetAccessResult> TryValidate(AssetId assetId, List<string> roles, AuthMechanism mechanism,
        CancellationToken cancellationToken = default);
}