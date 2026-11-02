using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
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
    public Task<DescriptionResourceResponse> Handle(ProbeService request, CancellationToken cancellationToken)
    {
        var assetId = request.AssetId;

        return ProbeServiceSupport.HandleProbeRequest(assetId, httpContextAccessor, logger,
            () => assetTracker.GetOrchestrationAsset(assetId),
            (asset, accessToken) =>
                iiifAuth2Client.GetProbeServiceResult(assetId, asset.Roles, accessToken, cancellationToken));
    }
}
