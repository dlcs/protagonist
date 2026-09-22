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
/// Handles IIIF Authorization Flow 2.0 ProbeService request for an adjunct
/// </summary>
/// <remarks>
/// Probe service will always return a 200 status code, the response will contain the status code the user will
/// receive if they make a request for the associated adjunct. Adjuncts inherit the roles of their parent Asset.
/// </remarks>
public class AdjunctProbeService(int customer, int space, string asset, string adjunctId)
    : IRequest<DescriptionResourceResponse>
{
    public AssetId AssetId { get; } = new(customer, space, asset);
    public string AdjunctId { get; } = adjunctId;
}

public class AdjunctProbeServiceHandler(
    IIIFAuth2Client iiifAuth2Client,
    IAdjunctTracker adjunctTracker,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AdjunctProbeServiceHandler> logger)
    : IRequestHandler<AdjunctProbeService, DescriptionResourceResponse>
{
    public Task<DescriptionResourceResponse> Handle(AdjunctProbeService request, CancellationToken cancellationToken)
    {
        var assetId = request.AssetId;
        var adjunctId = request.AdjunctId;

        return ProbeServiceSupport.HandleProbeRequest(assetId, httpContextAccessor, logger,
            () => adjunctTracker.GetOrchestrationAdjunct(adjunctId, assetId),
            (adjunct, accessToken) => iiifAuth2Client.GetProbeServiceResultForAdjunct(assetId, adjunctId,
                adjunct.Roles, accessToken, cancellationToken));
    }
}
