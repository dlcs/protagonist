using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Strings;
using DLCS.Core.Types;
using DLCS.Web;
using DLCS.Web.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Orchestrator.Features.Auth;

namespace Orchestrator.Infrastructure.Auth;

/// <summary>
/// Unified access validator that can check access via Auth v0/1 (ie Orchestrator managed) or Auth v2 (external
/// services). This may result in multiple checks being made
/// </summary>
public class AssetAccessValidator : IAssetAccessValidator
{
    private readonly Auth1AccessValidator auth1AccessValidator;
    private readonly Auth2AccessValidator auth2AccessValidator;
    private readonly AuthCookieManager authCookieManager;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<AssetAccessValidator> logger;

    public AssetAccessValidator(
        Auth1AccessValidator auth1AccessValidator, 
        Auth2AccessValidator auth2AccessValidator,
        AuthCookieManager authCookieManager,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AssetAccessValidator> logger)
    {
        this.auth1AccessValidator = auth1AccessValidator;
        this.auth2AccessValidator = auth2AccessValidator;
        this.authCookieManager = authCookieManager;
        this.httpContextAccessor = httpContextAccessor;
        this.logger = logger;
    }

    public async Task<AssetAccessResult> TryValidate(AssetId assetId, List<string> roles, AuthMechanism mechanism, CancellationToken cancellationToken = default)
    {
        if (ShouldAttemptAuth1(assetId.Customer, mechanism))
        {
            var auth1Status = await auth1AccessValidator.TryValidate(assetId, roles, mechanism, cancellationToken);
            if (auth1Status == AssetAccessResult.Authorized)
            {
                logger.LogTrace("{AssetId} can be viewed via Auth1", assetId);
                return auth1Status;
            }
        }

        if (HasAuth2Cookie(assetId.Customer))
        {
            var auth2Status = await auth2AccessValidator.TryValidate(assetId, roles, mechanism, cancellationToken);
            if (auth2Status == AssetAccessResult.Authorized)
            {
                logger.LogTrace("{AssetId} can be viewed via Auth2", assetId);
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