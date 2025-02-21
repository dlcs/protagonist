using DLCS.Core.Types;

namespace Orchestrator.Features.Auth.Paths;

public interface IAuthPathGenerator
{
    /// <summary>
    /// Generate full auth path using specified params for template replacement
    /// </summary>
    string GetAuthPathForRequest(string customer, string behaviour);

    /// <summary>
    /// Generate full auth path for IIIF Auth Flow 2.0, using specified params for template replacement
    /// </summary>
    string GetAuth2PathForRequest(AssetId assetId, string iiifServiceType, string? accessServiceName);
}