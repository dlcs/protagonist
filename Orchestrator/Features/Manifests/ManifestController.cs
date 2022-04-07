using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Caching;
using IIIF.Presentation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Features.Manifests.Requests;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.IIIF.Conneg;
using Orchestrator.Settings;

namespace Orchestrator.Features.Manifests
{
    [Route("iiif-manifest")]
    [ApiController]
    public class ManifestController : IIIFAssetControllerBase
    {
        private readonly OrchestratorSettings orchestratorSettings;

        public ManifestController(
            IMediator mediator, 
            IOptions<CacheSettings> cacheSettings,
            IOptions<OrchestratorSettings> orchestratorSettings,
            ILogger<ManifestController> logger
            ) : base(mediator, cacheSettings, logger)
        {
            this.orchestratorSettings = orchestratorSettings.Value;
        }

        /// <summary>
        /// Get single-item manifest for specified asset. The IIIF Presentation version returned will depend on server
        /// configuration but can be specified via the Accepts header
        /// </summary>
        /// <param name="cancellationToken">Async cancellation token</param>
        /// <returns>IIIF manifest containing specified item</returns>
        [Route("{customer}/{space}/{image}")]
        [HttpGet]
        public Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var requestedVersion = Request.GetTypedHeaders().Accept.GetIIIFPresentationType();
            var version = requestedVersion == Version.Unknown
                ? orchestratorSettings.GetDefaultIIIFPresentationVersion()
                : requestedVersion;
            return RenderManifest(version, cancellationToken);
        }

        /// <summary>
        /// Get single-item manifest for specified asset as a IIIF Manifest conforming to v2.1.
        /// </summary>
        /// <param name="cancellationToken">Async cancellation token</param>
        /// <returns>IIIF manifest containing specified item</returns>
        [Route("v2/{customer}/{space}/{image}")]
        [HttpGet]
        public Task<IActionResult> V2(CancellationToken cancellationToken = default)
            => RenderManifest(Version.V2, cancellationToken);
        
        /// <summary>
        /// Get single-item manifest for specified asset as a IIIF Manifest conforming to v3.0.
        /// </summary>
        /// <param name="cancellationToken">Async cancellation token</param>
        /// <returns>IIIF manifest containing specified item</returns>
        [Route("v3/{customer}/{space}/{image}")]
        [HttpGet]
        public Task<IActionResult> V3(CancellationToken cancellationToken = default)
            => RenderManifest(Version.V3, cancellationToken);
        
        public Task<IActionResult> RenderManifest(Version presentationVersion, CancellationToken cancellationToken = default)
        {
            var contentType = presentationVersion == Version.V3 ? ContentTypes.V3 : ContentTypes.V2;
            return GenerateIIIFDescriptionResource(
                () => new GetManifestForAsset(HttpContext.Request.Path, presentationVersion),
                contentType,
                cancellationToken);
        }
    }
}