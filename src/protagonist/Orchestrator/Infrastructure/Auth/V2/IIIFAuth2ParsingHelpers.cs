using IIIF.Auth.V2;

namespace Orchestrator.Infrastructure.Auth.V2;

public static class IIIFAuth2ParsingHelpers
{
    /// <summary>
    /// Parse the default "id" value of AuthAccessService2 to derive access-service-name
    /// </summary>
    public static string? GetAccessServiceNameFromDefaultPath(this AuthAccessService2 accessService)
        => accessService.Id?.Split('/')[^1];

    /// <summary>
    /// Parse the default "id" value of AuthAccessService2 to derive access-service-name
    /// </summary>
    public static string? GetAccessServiceNameFromDefaultPath(this AuthLogoutService2 logoutService)
        => string.IsNullOrEmpty(logoutService.Id) ? logoutService.Id : logoutService.Id.Split('/')[^2];
}