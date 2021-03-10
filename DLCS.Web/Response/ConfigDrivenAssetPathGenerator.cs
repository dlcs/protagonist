using System.Text;
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

        public string GetPathForRequest(BaseAssetRequest assetRequest)
            => GetPathForRequestInternal(assetRequest, false);

        public string GetFullPathForRequest(BaseAssetRequest assetRequest)
            => GetPathForRequestInternal(assetRequest, true);
        
        private string GetPathForRequestInternal(BaseAssetRequest assetRequest, bool fullRequest)
        {
            var request = httpContextAccessor.HttpContext.Request;
            var host = request.Host.Value ?? string.Empty;
            
            var path = GeneratePathFromTemplate(assetRequest, host);

            return fullRequest ? GetDisplayUrl(request, host, path) : path;
        }

        private string GeneratePathFromTemplate(BaseAssetRequest assetRequest, string host)
        {
            var template = pathTemplateOptions.GetPathTemplateForHost(host);
            return template
                .Replace("{prefix}", assetRequest.RoutePrefix)
                .Replace("{customer}", assetRequest.CustomerPathValue)
                .Replace("{space}", assetRequest.Space.ToString())
                .Replace("{assetPath}", assetRequest.AssetPath);
        }

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