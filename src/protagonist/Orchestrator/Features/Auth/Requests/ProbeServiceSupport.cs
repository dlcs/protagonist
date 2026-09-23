using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Web;
using DLCS.Web.Auth;
using IIIF.Auth.V2;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.Auth.V2;
using Orchestrator.Models;

namespace Orchestrator.Features.Auth.Requests;

/// <summary>
/// Shared logic for evaluating IIIF Authorization Flow 2.0 ProbeService requests, used by both asset and adjunct
/// probe service handlers.
/// </summary>
public static class ProbeServiceSupport
{
    /// <summary>
    /// Handle a full probe service request for an item (asset or adjunct): checks for a bearer token, looks up the
    /// item, and evaluates the auth requirements, calling the downstream auth service if required.
    /// </summary>
    /// <param name="assetId">AssetId the request relates to, used for logging</param>
    /// <param name="httpContextAccessor">Used to read the bearer token from the current request</param>
    /// <param name="logger">Logger for the calling handler</param>
    /// <param name="lookupItem">Delegate to fetch the item (asset or adjunct) being probed</param>
    /// <param name="getDownstreamProbeResult">Delegate to call the downstream auth service for the found item</param>
    public static async Task<DescriptionResourceResponse> HandleProbeRequest<T>(
        AssetId assetId,
        IHttpContextAccessor httpContextAccessor,
        ILogger logger,
        Func<Task<T?>> lookupItem,
        Func<T, string, Task<AuthProbeResult2>> getDownstreamProbeResult)
        where T : class, IProbeableOrchestrationItem
    {
        var accessToken = GetAccessToken(httpContextAccessor);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            logger.LogDebug("ProbeService request for {AssetId} has no bearer token", assetId);
            return DescriptionResourceResponse.Restricted(AuthProbeResult2Builder.MissingCredentials);
        }

        var item = await lookupItem();
        if (item == null)
        {
            logger.LogDebug("ProbeService request for not-found {AssetId}", assetId);
            return DescriptionResourceResponse.Empty;
        }

        return await Resolve(item.RequiresAuth, item.Roles, assetId, logger,
            () => getDownstreamProbeResult(item, accessToken));
    }

    private static async Task<DescriptionResourceResponse> Resolve(
        bool requiresAuth,
        IReadOnlyList<string> roles,
        AssetId assetId,
        ILogger logger,
        Func<Task<AuthProbeResult2>> getDownstreamProbeResult)
    {
        if (!requiresAuth)
        {
            logger.LogDebug("ProbeService request for non auth {AssetId}", assetId);
            return DescriptionResourceResponse.Restricted(AuthProbeResult2Builder.Okay);
        }

        if (roles.IsNullOrEmpty())
        {
            logger.LogInformation("ProbeService request for auth {AssetId} with no roles", assetId);
            return DescriptionResourceResponse.Restricted(AuthProbeResult2Builder.Okay);
        }

        if (roles.ContainsOnly(Asset.UnobtainableRole))
        {
            logger.LogInformation("ProbeService request for auth {AssetId} with unobtainable role", assetId);
            return DescriptionResourceResponse.Restricted(AuthProbeResult2Builder.UnobtainableRole);
        }

        var authProbeResult = await getDownstreamProbeResult();
        return DescriptionResourceResponse.Restricted(authProbeResult);
    }

    private static string? GetAccessToken(IHttpContextAccessor httpContextAccessor)
    {
        var bearerToken = httpContextAccessor.SafeHttpContext().Request
            .GetAuthHeaderValue(AuthenticationHeaderUtils.BearerTokenScheme);
        return bearerToken?.Parameter;
    }
}
