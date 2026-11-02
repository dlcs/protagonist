using System.Threading;
using System.Threading.Tasks;
using Orchestrator.Assets;

namespace Orchestrator.Infrastructure.Auth;

public interface IAssetAccessValidator
{
    /// <summary>
    /// Validate whether current request has access to the specified item.
    /// This will try to validate request using specified <see cref="AuthMechanism"/>
    /// </summary>
    /// <param name="orchestrationItem">Asset or Adjunct to validate</param>
    /// <param name="mechanism">Which mechanism to use to authorize user</param>
    /// <returns><see cref="AssetAccessResult"/> enum representing result of validation</returns>
    Task<AssetAccessResult> TryValidate(IAccessControlledOrchestrationItem orchestrationItem, AuthMechanism mechanism,
        CancellationToken cancellationToken = default);
}
