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
/// This class uses <see cref="PathOverrides"/> to determine different URL patterns for different hostnames,
/// this allows e.g. "id" values on manifests to use different URL structures than the default DLCS paths.
/// e.g. /images/{image}/ rather than default of /iiif-img/{cust}/{space}/{image} 
/// </remarks>
public class ConfigDrivenAssetPathGenerator : IAssetPathGenerator
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly PathTemplateOptions pathTemplateOptions;

    public ConfigDrivenAssetPathGenerator(
        IOptions<PathTemplateOptions> pathTemplateOptions,
        IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.pathTemplateOptions = pathTemplateOptions.Value;
    }

    public string GetFullPathForRequest(IBasicPathElements assetRequest, bool useNativeFormat = false)
        => GetForPath(assetRequest, true, useNativeFormat);

    public string GetFullPathForRequest(IBasicPathElements assetRequest, PathGenerator pathGenerator,
        bool useNativeFormat = false)
        => GetPathForRequestInternal(assetRequest, pathGenerator, true, useNativeFormat);

    private string GetForPath(IBasicPathElements assetRequest, bool fullRequest, bool useNativeFormat)
        => GetPathForRequestInternal(
            assetRequest, 
            (request, template) => GeneratePathFromTemplate(request, template),
            fullRequest,
            useNativeFormat);
    
    private string GetPathForRequestInternal(IBasicPathElements assetRequest, PathGenerator pathGenerator,
        bool fullRequest, bool useNativeFormat)
    {
        const string dlcsNativeFormat = "/{prefix}/{customer}/{space}/{assetPath}";
        
        var request = httpContextAccessor.HttpContext.Request;
        var host = request.Host.Value ?? string.Empty;
        var template = useNativeFormat ? dlcsNativeFormat : pathTemplateOptions.GetPathTemplateForHost(host);

        var path = pathGenerator(assetRequest, template);

        return fullRequest ? request.GetDisplayUrl(path) : path;
    }

    // Default path replacements
    private string GeneratePathFromTemplate(IBasicPathElements assetRequest, string template) 
        => DlcsPathHelpers.GeneratePathFromTemplate(template,
            prefix: assetRequest.RoutePrefix,
            customer: assetRequest.CustomerPathValue,
            space: assetRequest.Space.ToString(),
            assetPath: assetRequest.AssetPath);
}

