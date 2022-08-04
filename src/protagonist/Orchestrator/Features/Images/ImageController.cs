using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Web.IIIF;
using IIIF.ImageApi;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Features.Images.Requests;
using Orchestrator.Infrastructure;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images;

[Route("iiif-img")]
[ApiController]
public class ImageController : IIIFAssetControllerBase
{
    private const string CanonicalInfoJsonRoute = "info_json_canonical";
    private readonly OrchestratorSettings orchestratorSettings;

    public ImageController(
        IMediator mediator, 
        IOptions<CacheSettings> cacheSettings,
        IOptions<OrchestratorSettings> orchestratorSettings,
        ILogger<ImageController> logger) : base(mediator, cacheSettings, logger)
    {
        this.orchestratorSettings = orchestratorSettings.Value;
    }

    /// <summary>
    /// Index request for image root, redirects to info.json.
    /// </summary>
    /// <returns></returns>
    [Route("{customer}/{space}/{image}", Name = "image_only")]
    [HttpGet]
    public IActionResult ImageOnly()
        => RedirectToInfoJson();
    
    /// <summary>
    /// Index request for image root, redirects to info.json.
    /// Matches a version string in format "v2" or "v3"
    /// </summary>
    /// <returns></returns>
    [Route("{version:regex(^v2|v3$)}/{customer}/{space}/{image}", Name = "image_only_versioned")]
    [HttpGet]
    public IActionResult ImageOnlyVersioned()
    {
        var requestedVersion = Request.GetIIIFImageApiVersionFromRoute();

        // Requesting image-only for canonical version, redirect to canonical info.json
        if (IsCanonicalRequest(requestedVersion))
        {
            var routeUrl = Url.RouteUrl(CanonicalInfoJsonRoute, new
            {
                customer = Request.RouteValues["customer"],
                space = Request.RouteValues["space"],
                image = Request.RouteValues["image"]
            })!;
            return SeeAlsoResult(routeUrl);
        }
        
        return RedirectToInfoJson();
    }

    /// <summary>
    /// Get info.json file for specified image
    /// </summary>
    /// <param name="noOrchestrate">
    /// Optional query parameter, if true then info.json request will not trigger orchestration
    /// </param>
    /// <param name="cancellationToken">Async cancellation token</param>
    /// <returns>IIIF info.json for specified manifest.</returns>
    [Route("{customer}/{space}/{image}/info.json", Name = CanonicalInfoJsonRoute)]
    [HttpGet]
    public Task<IActionResult> InfoJson([FromQuery] bool noOrchestrate = false,
        CancellationToken cancellationToken = default)
    {
        var version = Request.GetIIIFImageApiVersion(orchestratorSettings.DefaultIIIFImageVersion);
        return RenderInfoJson(version, noOrchestrate, cancellationToken);
    }

    /// <summary>
    /// Get info.json file for specified image confirming to ImageApi v2 or v3, depending on version route value.
    /// Accepted values are "v2" and "v3"
    /// </summary>
    /// <param name="noOrchestrate">
    /// Optional query parameter, if true then info.json request will not trigger orchestration
    /// </param>
    /// <param name="cancellationToken">Async cancellation token</param>
    /// <returns>IIIF info.json for specified manifest.</returns>
    [Route("{version:regex(^v2|v3$)}/{customer}/{space}/{image}/info.json", Name = "info_json_versioned")]
    [HttpGet]
    public async Task<IActionResult> InfoJsonVersioned([FromQuery] bool noOrchestrate = false,
        CancellationToken cancellationToken = default)
    {
        var requestedVersion = Request.GetIIIFImageApiVersionFromRoute();
        if (IsCanonicalRequest(requestedVersion))
        {
            return RedirectToRoute(CanonicalInfoJsonRoute, new
            {
                customer = Request.RouteValues["customer"],
                space = Request.RouteValues["space"],
                image = Request.RouteValues["image"]
            });
        }

        if (!requestedVersion.HasValue)
        {
            return BadRequest("Unknown iiif image api version requested");
        }

        return await RenderInfoJson(requestedVersion.Value, noOrchestrate, cancellationToken);
    }

    private StatusCodeResult RedirectToInfoJson()
    {
        var location = HttpContext.Request.Path.Add("/info.json");
        return SeeAlsoResult(location);
    }

    private StatusCodeResult SeeAlsoResult(string location)
    {
        Response.Headers["Location"] = location;
        return new StatusCodeResult(303);
    }

    private Task<IActionResult> RenderInfoJson(Version imageApiVersion, bool noOrchestrate,
        CancellationToken cancellationToken)
    {
        var contentType = Request.GetIIIFContentType(imageApiVersion);
        return GenerateIIIFDescriptionResource(
            () => new GetImageInfoJson(HttpContext.Request.Path, imageApiVersion, noOrchestrate),
            contentType,
            cancellationToken);
    }
    
    private bool IsCanonicalRequest(Version? requestedVersion) 
        => requestedVersion.HasValue && requestedVersion == orchestratorSettings.DefaultIIIFImageVersion;
}