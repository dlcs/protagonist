using System.Text;
using DLCS.Core;
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
        private const string SchemeDelimiter = "://";

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

            return fullRequest ? GetDisplayUrl(request, host, path) : path;
        }

        private string GeneratePathFromTemplate(IBasicPathElements assetRequest, string template) 
            => DlcsPathHelpers.GeneratePathFromTemplate(template,
                prefix: assetRequest.RoutePrefix,
                customer: assetRequest.CustomerPathValue,
                space: assetRequest.Space.ToString(),
                assetPath: assetRequest.AssetPath);

        // based on Microsoft.AspNetCore.Http.Extensions.UriHelper.GetDisplayUrl(this HttpRequest request)
        private static string GetDisplayUrl(HttpRequest request, string host, string path)
        {
            var scheme = request.Scheme ?? string.Empty;
            var pathBase = request.PathBase.Value ?? string.Empty;
            var queryString = request.QueryString.Value ?? string.Empty;

            // PERF: Calculate string length to allocate correct buffer size for StringBuilder.
            var length = scheme.Length + SchemeDelimiter.Length + host.Length
                         + pathBase.Length + path.Length + queryString.Length;

            return new StringBuilder(length)
                .Append(scheme)
                .Append(SchemeDelimiter)
                .Append(host)
                .Append(pathBase)
                .Append(path)
                .Append(queryString)
                .ToString();
        }
    }
}