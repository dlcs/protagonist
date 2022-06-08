using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Caching;
using IIIF.ImageApi;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Features.Images.Requests;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images
{
    [Route("iiif-img")]
    [ApiController]
    public class ImageController : IIIFAssetControllerBase
    {
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
        /// Matches a version string in format vX, where X is a single digit
        /// </summary>
        /// <returns></returns>
        [Route("{version:regex(^v\\d$)}/{customer}/{space}/{image}", Name = "image_only_versioned")]
        [HttpGet]
        public IActionResult ImageOnlyVersioned()
            => RedirectToInfoJson();

        /// <summary>
        /// Get info.json file for specified image
        /// </summary>
        /// <param name="noOrchestrate">
        /// Optional query parameter, if true then info.json request will not trigger orchestration
        /// </param>
        /// <param name="cancellationToken">Async cancellation token</param>
        /// <returns>IIIF info.json for specified manifest.</returns>
        [Route("{customer}/{space}/{image}/info.json", Name = "info_json")]
        [HttpGet]
        public Task<IActionResult> InfoJson([FromQuery] bool noOrchestrate = false,
            CancellationToken cancellationToken = default)
        {
            var version = GetRequestIIIFImageApiVersion();
            return RenderInfoJson(version, noOrchestrate, cancellationToken);
        }

        /// <summary>
        /// Get info.json file for specified image confirming to ImageApi v2.1
        /// </summary>
        /// <param name="noOrchestrate">
        /// Optional query parameter, if true then info.json request will not trigger orchestration
        /// </param>
        /// <param name="cancellationToken">Async cancellation token</param>
        /// <returns>v2.1 IIIF info.json for specified manifest.</returns>
        [Route("v2/{customer}/{space}/{image}/info.json", Name = "info_json_v2")]
        [HttpGet]
        public Task<IActionResult> InfoJsonV2([FromQuery] bool noOrchestrate = false,
            CancellationToken cancellationToken = default) =>
            RenderInfoJson(Version.V2, noOrchestrate, cancellationToken);
        
        /// <summary>
        /// Get info.json file for specified image confirming to ImageApi v3.0
        /// </summary>
        /// <param name="noOrchestrate">
        /// Optional query parameter, if true then info.json request will not trigger orchestration
        /// </param>
        /// <param name="cancellationToken">Async cancellation token</param>
        /// <returns>v3.0 IIIF info.json for specified manifest.</returns>
        [Route("v3/{customer}/{space}/{image}/info.json", Name = "info_json_v3")]
        [HttpGet]
        public Task<IActionResult> InfoJsonV3([FromQuery] bool noOrchestrate = false,
            CancellationToken cancellationToken = default) =>
            RenderInfoJson(Version.V3, noOrchestrate, cancellationToken);
        
        private StatusCodeResult RedirectToInfoJson()
        {
            var location = HttpContext.Request.Path.Add("/info.json");
            Response.Headers["Location"] = location.Value;
            return new StatusCodeResult(303);
        }

        private Version GetRequestIIIFImageApiVersion()
        {
            var requestedVersion = Request.GetTypedHeaders().Accept.GetIIIFImageApiType();
            var version = requestedVersion == Version.Unknown
                ? orchestratorSettings.GetDefaultIIIFImageVersion()
                : requestedVersion;
            return version;
        }

        private Task<IActionResult> RenderInfoJson(Version imageApiVersion, bool noOrchestrate,
            CancellationToken cancellationToken)
        {
            var contentType = imageApiVersion == Version.V3 ? ContentTypes.V3 : ContentTypes.V2;
            return GenerateIIIFDescriptionResource(
                () => new GetImageInfoJson(HttpContext.Request.Path, imageApiVersion, noOrchestrate),
                contentType,
                cancellationToken);
        }
    }
}