using DLCS.Core;
using DLCS.Web.Requests;
using DLCS.Web.Requests.AssetDelivery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DLCS.Web.Response
{
    /// <summary>
    /// Generate paths related to running Dlcs instance using appSettings config for rules.
    /// </summary>
    public class ConfigDrivenAssetPathGenerator : IAssetPathGenerator
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly PathTemplateOptions pathTemplateOptions;

        public ConfigDrivenAssetPathGenerator(IOptions<PathTemplateOptions> pathTemplateOptions,
            IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.pathTemplateOptions = pathTemplateOptions.Value;
        }

        public string GetPathForRequest(IBasicPathElements assetRequest)
            => GetForPath(assetRequest, false);

        public string GetFullPathForRequest(IBasicPathElements assetRequest)
            => GetForPath(assetRequest, true);

        public string GetFullPathForRequest(IBasicPathElements assetRequest, PathGenerator pathGenerator)
            => GetPathForRequestInternal(assetRequest, pathGenerator, true);

        private string GetForPath(IBasicPathElements assetRequest, bool fullRequest)
            => GetPathForRequestInternal(
                assetRequest, 
                (request, template) => GeneratePathFromTemplate(request, template),
                fullRequest);
        
        private string GetPathForRequestInternal(IBasicPathElements assetRequest, PathGenerator pathGenerator,
            bool fullRequest)
        {
            var request = httpContextAccessor.HttpContext.Request;
            var host = request.Host.Value ?? string.Empty;
            var template = pathTemplateOptions.GetPathTemplateForHost(host);

            var path = pathGenerator(assetRequest, template);

            return fullRequest ? request.GetDisplayUrl(path) : path;
        }

        private string GeneratePathFromTemplate(IBasicPathElements assetRequest, string template) 
            => DlcsPathHelpers.GeneratePathFromTemplate(template,
                prefix: assetRequest.RoutePrefix,
                customer: assetRequest.CustomerPathValue,
                space: assetRequest.Space.ToString(),
                assetPath: assetRequest.AssetPath);
    }
}