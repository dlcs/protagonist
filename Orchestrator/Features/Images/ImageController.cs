using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Caching;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Features.Images.Requests;
using Orchestrator.Infrastructure;

namespace Orchestrator.Features.Images
{
    [Route("iiif-img/{customer}/{space}/{image}")]
    [ApiController]
    public class ImageController : IIIFAssetControllerBase
    {
        public ImageController(
            IMediator mediator, 
            IOptions<CacheSettings> cacheSettings,
            ILogger<ImageController> logger) : base(mediator, cacheSettings, logger)
        {
        }

        /// <summary>
        /// Index request for image root, redirects to info.json.
        /// </summary>
        /// <returns></returns>
        [Route("", Name = "image_only")]
        [HttpGet]
        public IActionResult Index()
        {
            var location = HttpContext.Request.Path.Add("/info.json");
            Response.Headers["Location"] = location.Value;
            return new StatusCodeResult(303);
        }

        /// <summary>
        /// Get info.json file for specified image
        /// </summary>
        /// <param name="noOrchestrate">
        /// Optional query parameter, if true then info.json request will not trigger orchestration
        /// </param>
        /// <param name="cancellationToken">Async cancellation token</param>
        /// <returns>IIIF info.json for specified manifest.</returns>
        [Route("info.json", Name = "info_json")]
        [HttpGet]
        public Task<IActionResult> InfoJson([FromQuery] bool noOrchestrate = false,
            CancellationToken cancellationToken = default)
            => GenerateIIIFDescriptionResource(() => new GetImageInfoJson(HttpContext.Request.Path, noOrchestrate),
                cancellationToken);
    }
}