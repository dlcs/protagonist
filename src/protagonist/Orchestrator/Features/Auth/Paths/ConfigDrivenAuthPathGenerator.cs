using DLCS.Core;
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
        var request = httpContextAccessor.HttpContext.Request;
        var host = request.Host.Value ?? string.Empty;
        var template = orchestratorSettings.Auth.AuthPathRules.GetPathTemplateForHost(host);

        var path = DlcsPathHelpers.GenerateAuthPathFromTemplate(template, customer, behaviour);
        return request.GetDisplayUrl(path);
    }
}