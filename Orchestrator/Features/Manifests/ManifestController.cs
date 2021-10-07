using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Caching;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Features.Manifests.Requests;
using Orchestrator.Infrastructure;

namespace Orchestrator.Features.Manifests
{
    [Route("iiif-manifest")]
    [ApiController]
    public class ManifestController : IIIFAssetControllerBase
    {
        public ManifestController(
            IMediator mediator, 
            IOptions<CacheSettings> cacheSettings,
            ILogger<ManifestController> logger
            ) : base(mediator, cacheSettings, logger)
        {
        }

        /// <summary>
        /// Get single-item manifest for specified asset
        /// </summary>
        /// <param name="cancellationToken">Async cancellation token</param>
        /// <returns>IIIF manifest containing specified item</returns>
        [Route("{customer}/{space}/{image}")]
        [HttpGet]
        public Task<IActionResult> Index(CancellationToken cancellationToken = default)
            => GenerateIIIFJsonResponse(() => new GetManifestForAsset(HttpContext.Request.Path), cancellationToken);
    }
}