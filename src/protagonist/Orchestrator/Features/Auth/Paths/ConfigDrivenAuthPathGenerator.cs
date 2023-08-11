using DLCS.Core;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Web;
using DLCS.Web.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;

namespace Orchestrator.Features.Auth.Paths;

public class ConfigDrivenAuthPathGenerator : IAuthPathGenerator
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly OrchestratorSettings orchestratorSettings;

    public ConfigDrivenAuthPathGenerator(
        IOptions<OrchestratorSettings> orchestratorSettings,
        IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.orchestratorSettings = orchestratorSettings.Value;
    }

    public string GetAuthPathForRequest(string customer, string behaviour)
    {
        var request = GetHttpRequest();
        var host = request.Host.Value;
        var template = orchestratorSettings.Auth.AuthPathRules.GetPathTemplateForHost(host);

        var path = DlcsPathHelpers.GenerateAuthPathFromTemplate(template, customer, behaviour);
        return request.GetDisplayUrl(path);
    }

    public string GetAuth2PathForRequest(AssetId assetId, string iiifServiceType, string? accessServiceName)
    {
        var request = GetHttpRequest();
        var host = request.Host.Value;
        var template = orchestratorSettings.Auth.Auth2PathRules.GetPathTemplateForHostAndType(host, iiifServiceType);

        var path = DlcsPathHelpers.GenerateAuth2PathFromTemplate(template, assetId, assetId.Customer.ToString(),
            accessServiceName);
        return request.GetDisplayUrl(path);
    }
    
    private HttpRequest GetHttpRequest()
    {
        var request = httpContextAccessor.SafeHttpContext().Request;
        return request;
    }
}