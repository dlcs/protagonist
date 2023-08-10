using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Types;
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
public class ProbeService : IRequest<DescriptionResourceResponse>
{
    public AssetId AssetId { get; }

    public ProbeService(int customer, int space, string asset)
    {
        AssetId = new AssetId(customer, space, asset);
    }
}

public class ProbeServiceHandler : IRequestHandler<ProbeService, DescriptionResourceResponse>
{
    private readonly IIIFAuth2Client iiifAuth2Client;
    private readonly IAssetTracker assetTracker;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<ProbeServiceHandler> logger;

    public ProbeServiceHandler(
        IIIFAuth2Client iiifAuth2Client,
        IAssetTracker assetTracker,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ProbeServiceHandler> logger)
    {
        this.iiifAuth2Client = iiifAuth2Client;
        this.assetTracker = assetTracker;
        this.httpContextAccessor = httpContextAccessor;
        this.logger = logger;
    }
    
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