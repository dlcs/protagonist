using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Web;
using DLCS.Web.Auth;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.Auth.V2;
using Orchestrator.Models;

namespace Orchestrator.Features.Auth.Requests;

/// <summary>
/// Handles IIIF Authorization Flow 2.0 ProbeService request
/// </summary>
/// <remarks>
/// Probe service will always return a 200 status code, the response will contain the status code the user will receive
/// if they make a request for the associated asset.
/// </remarks>
public class ProbeService(int customer, int space, string asset) : IRequest<DescriptionResourceResponse>
{
    public AssetId AssetId { get; } = new(customer, space, asset);
}

public class ProbeServiceHandler(
    IIIFAuth2Client iiifAuth2Client,
    IAssetTracker assetTracker,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ProbeServiceHandler> logger)
    : IRequestHandler<ProbeService, DescriptionResourceResponse>
{
    public async Task<DescriptionResourceResponse> Handle(ProbeService request, CancellationToken cancellationToken)
    {
        var assetId = request.AssetId;
        
        var accessToken = GetAccessToken();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            logger.LogDebug("ProbeService request for {AssetId} has no bearer token", assetId);
            return DescriptionResourceResponse.Restricted(AuthProbeResult2Builder.MissingCredentials); 
        }

        var asset = await assetTracker.GetOrchestrationAsset(assetId);
        if (asset == null)
        {
            logger.LogDebug("ProbeService request for not-found {AssetId}", assetId);
            return DescriptionResourceResponse.Empty;
        }

        if (!asset.RequiresAuth)
        {
            logger.LogDebug("ProbeService request for non auth asset {AssetId}", assetId);
            return DescriptionResourceResponse.Restricted(AuthProbeResult2Builder.Okay);
        }

        if (asset.Roles.IsNullOrEmpty())
        {
            logger.LogInformation("ProbeService request for auth asset {AssetId} with no roles", assetId);
            return DescriptionResourceResponse.Restricted(AuthProbeResult2Builder.Okay);
        }

        if (asset.Roles.ContainsOnly(Asset.UnobtainableRole))
        {
            logger.LogInformation("ProbeService request for auth asset {AssetId} with unobtainable role", assetId);
            return DescriptionResourceResponse.Restricted(AuthProbeResult2Builder.UnobtainableRole);
        }

        var authProbeResult =
            await iiifAuth2Client.GetProbeServiceResult(assetId, asset.Roles, accessToken, cancellationToken);
        return DescriptionResourceResponse.Restricted(authProbeResult);
    }

    private string? GetAccessToken()
    {
        var bearerToken = httpContextAccessor.SafeHttpContext().Request
            .GetAuthHeaderValue(AuthenticationHeaderUtils.BearerTokenScheme);
        return bearerToken?.Parameter;
    }
}
