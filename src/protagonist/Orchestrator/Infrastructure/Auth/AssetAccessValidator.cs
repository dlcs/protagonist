using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Web;
using DLCS.Web.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Features.Auth;

namespace Orchestrator.Infrastructure.Auth;

/// <summary>
/// Unified access validator that can check access via Auth v0/1 (ie Orchestrator managed) or Auth v2 (external
/// services). This may result in multiple checks being made
/// </summary>
public class AssetAccessValidator(
    Auth1AccessValidator auth1AccessValidator,
    Auth2AccessValidator auth2AccessValidator,
    AuthCookieManager authCookieManager,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AssetAccessValidator> logger)
    : IAssetAccessValidator
{
    public async Task<AssetAccessResult> TryValidate(IAccessControlledOrchestrationItem orchestrationItem,
        AuthMechanism mechanism, CancellationToken cancellationToken = default)
    {
        var roles = orchestrationItem.Roles;
        var deliverableId = orchestrationItem.DeliverableId;
        if (roles.ContainsOnly(Asset.UnobtainableRole))
        {
            logger.LogTrace("{DeliverableId} only has unobtainable role, shortcutting check", deliverableId);
            return AssetAccessResult.Unauthorized;
        }

        var customer = deliverableId.AssetId.Customer;

        // Adjuncts can only be validated via Auth2
        if (!deliverableId.IsAdjunct && ShouldAttemptAuth1(customer, mechanism))
        {
            var auth1Status = await auth1AccessValidator.TryValidate(orchestrationItem, mechanism, cancellationToken);
            if (auth1Status == AssetAccessResult.Authorized)
            {
                logger.LogTrace("{DeliverableId} can be viewed via Auth1", deliverableId);
                return auth1Status;
            }
        }

        if (HasAuth2Cookie(customer))
        {
            var auth2Status = await auth2AccessValidator.TryValidate(orchestrationItem, mechanism, cancellationToken);
            if (auth2Status == AssetAccessResult.Authorized)
            {
                logger.LogTrace("{DeliverableId} can be viewed via Auth2", deliverableId);
                return auth2Status;
            }
        }

        return AssetAccessResult.Unauthorized;
    }

    private bool HasAuth1Cookie(int customer) => authCookieManager.GetCookieValueForCustomer(customer).HasText();
    private bool HasAuth2Cookie(int customer) => authCookieManager.HasAuth2CookieForCustomer(customer);

    private bool ShouldAttemptAuth1(int customer, AuthMechanism mechanism)
    {
        if (mechanism is AuthMechanism.All or AuthMechanism.Cookie)
        {
            if (HasAuth1Cookie(customer)) return true;
        }

        return mechanism is AuthMechanism.All or AuthMechanism.BearerToken && HasBearerToken();
    }
    
    private bool HasBearerToken()
        => httpContextAccessor.SafeHttpContext().Request
            .GetAuthHeaderValue(AuthenticationHeaderUtils.BearerTokenScheme) != null;
}

/// <summary>
/// Enum representing various results for attempting to access an asset.
/// </summary>
public enum AssetAccessResult
{
    /// <summary>
    /// Asset is open
    /// </summary>
    Open,
    
    /// <summary>
    /// Asset is restricted and current user does not have appropriate access
    /// </summary>
    Unauthorized,
    
    /// <summary>
    /// Asset is restricted and current user has access
    /// </summary>
    Authorized
}

/// <summary>
/// Enum representing different mechanisms for authorising users
/// </summary>
public enum AuthMechanism
{
    /// <summary>
    /// Auth user by cookie provided with request
    /// </summary>
    Cookie,
    
    /// <summary>
    /// Auth user by bearer token provided with request
    /// </summary>
    BearerToken,
    
    /// <summary>
    /// Try all possible methods of validation
    /// </summary>
    All
}
