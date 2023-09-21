using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using Orchestrator.Infrastructure.Auth.V2;

namespace Orchestrator.Infrastructure.Auth;

/// <summary>
/// <see cref="IAssetAccessValidator"/> that uses external service to validate access via IIIF Auth v2
/// </summary>
public class Auth2AccessValidator : IAssetAccessValidator
{
    private readonly IIIFAuth2Client iiifAuth2Client;

    public Auth2AccessValidator(IIIFAuth2Client iiifAuth2Client)
    {
        this.iiifAuth2Client = iiifAuth2Client;
    }
    
    public async Task<AssetAccessResult> TryValidate(AssetId assetId, List<string> roles, AuthMechanism mechanism,
        CancellationToken cancellationToken = default)
    {
        // NOTE(DG) - caller of this has checked appropriate cookie exists
        if (mechanism == AuthMechanism.BearerToken) return AssetAccessResult.Unauthorized;

        var canAccess = await iiifAuth2Client.VerifyAccess(assetId, roles, cancellationToken);

        return canAccess ? AssetAccessResult.Authorized : AssetAccessResult.Unauthorized;
    }
}