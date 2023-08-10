using System.Collections.Generic;
using System.Threading;
using DLCS.Core.Types;
using IIIF;
using IIIF.Auth.V2;
using Orchestrator.Infrastructure.IIIF;

namespace Orchestrator.Tests.Integration;

public class FakeAuth2Client : IIIIFAuthBuilder
{
    public Task<IService> GetAuthServicesForAsset(AssetId assetId, List<string> roles, CancellationToken cancellationToken = default)
    {
        var probeService = new AuthProbeService2
        {
            Id = $"http://localhost/auth/v2/probe/{assetId}",
            Service = new List<IService>
            {
                new AuthAccessService2
                {
                    Id = $"http://localhost/auth/v2/access/{assetId.Customer}/clickthrough",
                    Profile = "active",
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2
                        {
                            Id = $"http://localhost/auth/v2/access/{assetId.Customer}/token",
                        },
                        new AuthLogoutService2
                        {
                            Id = $"http://localhost/auth/v2/access/{assetId.Customer}/clickthrough/logout",
                        }
                    }
                }
            }
        };

        return Task.FromResult((IService)probeService);
    }
}