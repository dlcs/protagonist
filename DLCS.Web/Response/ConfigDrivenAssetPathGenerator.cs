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

        public string GetPathForRequest(BaseAssetRequest assetRequest)
        {
            var host = httpContextAccessor.HttpContext.Request.Host;
            
            var template = pathTemplateOptions.GetPathTemplateForHost(host.Host);
            return template
                .Replace("{prefix}", assetRequest.RoutePrefix)
                .Replace("{customer}", assetRequest.CustomerPathValue)
                .Replace("{space}", assetRequest.Space.ToString())
                .Replace("{assetPath}", assetRequest.AssetPath);
        }
    }
}