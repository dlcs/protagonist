using DLCS.Core;
using DLCS.Web.Requests;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DLCS.Web.Response;

/// <summary>
/// Generate paths related to running Dlcs instance using appSettings config for rules.
/// </summary>
/// <remarks>
/// This class uses <see cref="PathTemplateOptions"/> to determine different URL patterns for different hostnames,
/// this allows e.g. "id" values on manifests to use different URL structures than the default DLCS paths.
/// e.g. /images/{assetPath}/ rather than default of /iiif-img/{cust}/{space}/{assetPath} 
/// </remarks>
public class ConfigDrivenAssetPathGenerator(
    IOptions<PathTemplateOptions> pathTemplateOptions,
    IHttpContextAccessor httpContextAccessor) : IAssetPathGenerator
{
    private readonly PathTemplateOptions pathTemplateOptions = pathTemplateOptions.Value;

    public string GetRelativePathForRequest(IBasicPathElements assetRequest, bool useNativeFormat = false)
        => GetForPath(assetRequest, false, useNativeFormat, false);

    public string GetFullPathForRequest(IBasicPathElements assetRequest, bool useNativeFormat = false,
        bool includeQueryParams = true)
        => GetForPath(assetRequest, true, useNativeFormat, includeQueryParams);

    public bool PathHasVersion()
    {
        var request = httpContextAccessor.SafeHttpContext().Request;
        var template = pathTemplateOptions.GetPathTemplateForHost(request.Host.Value);
        return template.Path.Contains(DlcsPathHelpers.Replacements.Version);
    }

    private string GetForPath(IBasicPathElements assetRequest, bool fullRequest, bool useNativeFormat,
        bool includeQueryParams)
        => GetPathForRequestInternal(
            assetRequest,
            GeneratePathFromTemplate,
            fullRequest,
            useNativeFormat,
            includeQueryParams);

    private string GetPathForRequestInternal(IBasicPathElements assetRequest, PathGenerator pathGenerator,
        bool fullRequest, bool useNativeFormat, bool includeQueryParams)
    {
        var request = httpContextAccessor.SafeHttpContext().Request;
        var template = useNativeFormat
            ? PathTemplateOptions.DefaultPathTemplate
            : pathTemplateOptions.GetPathTemplateForHost(request.Host.Value);

        var path = pathGenerator(assetRequest, template);

        return fullRequest ? request.GetDisplayUrl(path, includeQueryParams) : path;
    }

    // Default path replacements
    private static string GeneratePathFromTemplate(IBasicPathElements assetRequest, PathTemplate template)
        => DlcsPathHelpers.GeneratePathFromTemplate(template.Path,
            prefix: template.GetPrefixForPath(assetRequest.RoutePrefix),
            customer: assetRequest.CustomerPathValue,
            version: assetRequest.VersionPathValue,
            space: assetRequest.Space.ToString(),
            assetPath: assetRequest.AssetPath);
}

