using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Web.IIIF;
using DLCS.Web.Requests.AssetDelivery;
using DLCS.Web.Response;
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
    private readonly IAssetPathGenerator assetPathGenerator;
    private const string CanonicalInfoJsonRoute = "info_json_canonical";
    private readonly OrchestratorSettings orchestratorSettings;

    public ImageController(
        IMediator mediator, 
        IOptions<CacheSettings> cacheSettings,
        ILogger<ImageController> logger,
        IAssetPathGenerator assetPathGenerator,
        IOptions<OrchestratorSettings> orchestratorSettings) : base(mediator, cacheSettings, logger)
    {
        this.assetPathGenerator = assetPathGenerator;
        this.orchestratorSettings = orchestratorSettings.Value;
    }

    /// <summary>
    /// Index request for image root, redirects to info.json.
    /// </summary>
    /// <returns></returns>
    [Route("{customer}/{space}/{image}", Name = "image_only")]
    [HttpGet]
    public IActionResult ImageOnly(
        [FromRoute] string customer,
        [FromRoute] int space,
        [FromRoute] string image)
        => ImageOnlyVersioned(customer, space, image);
    
    /// <summary>
    /// Index request for image root, redirects to info.json.
    /// Matches a version string in format "v2" or "v3"
    /// </summary>
    /// <returns></returns>
    [Route("{version:regex(^v2|v3$)}/{customer}/{space}/{image}", Name = "image_only_versioned")]
    [HttpGet]
    public IActionResult ImageOnlyVersioned(
        [FromRoute] string customer,
        [FromRoute] int space,
        [FromRoute] string image)
    {
        var requestedVersion = Request.GetIIIFImageApiVersionFromRoute();
        
        var basicPathElements = new BasicPathElements
        {
            RoutePrefix = "iiif-img",
            Space = space,
            VersionPathValue = requestedVersion?.ToString().ToLower(),
            CustomerPathValue = customer,
            AssetPath = $"{image}/info.json",
        };

        // Requesting image-only for canonical version, redirect to canonical info.json
        if (IsCanonicalRequest(requestedVersion))
        {
            basicPathElements.VersionPathValue = null;
        }

        var infoJson = assetPathGenerator.GetRelativePathForRequest(basicPathElements);
        return this.SeeAlsoResult(infoJson);
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
    public async Task<IActionResult> InfoJsonVersioned(
        [FromRoute] string customer,
        [FromRoute] int space,
        [FromRoute] string image,
        [FromQuery] bool noOrchestrate = false,
        CancellationToken cancellationToken = default)
    {
        var requestedVersion = Request.GetIIIFImageApiVersionFromRoute();
        if (IsCanonicalRequest(requestedVersion))
        {
            var basicPathElements = new BasicPathElements
            {
                RoutePrefix = "iiif-img",
                Space = space,
                CustomerPathValue = customer,
                AssetPath = $"{image}/info.json",
            };
            var canonicalRoute = assetPathGenerator.GetRelativePathForRequest(basicPathElements);
            return Redirect(canonicalRoute);
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
        return this.SeeAlsoResult(location);
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