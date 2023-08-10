using System.Linq;
using IIIF;
using IIIF.Auth.V2;

namespace Orchestrator.Infrastructure.IIIF;

public static class AuthProbeService2Converter
{
    /// <summary>
    /// Convert a 'full' <see cref="AuthProbeService2"/> into a reduced version for embedding into ImageService
    /// </summary>
    public static AuthProbeService2 ToEmbeddedService(this AuthProbeService2 fullService)
    {
        var embedded = new AuthProbeService2
        {
            Id = fullService.Id,
            Service = fullService.Service?
                .OfType<AuthAccessService2>()
                .Select(s => (IService)new AuthAccessService2
                {
                    Id = s.Id
                }).ToList()
        };

        return embedded;
    }
}