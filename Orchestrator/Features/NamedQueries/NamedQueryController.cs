using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Caching;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Features.NamedQueries.Requests;
using Orchestrator.Infrastructure;

namespace Orchestrator.Features.NamedQueries
{
    [Route("iiif-resource")]
    [ApiController]
    public class NamedQueryController : IIIFAssetControllerBase
    {
        public NamedQueryController(
            IMediator mediator, 
            IOptions<CacheSettings> cacheSettings,
            ILogger<NamedQueryController> logger
        ) : base(mediator, cacheSettings, logger)
        {
        }

        /// <summary>
        /// Get results of named query with specified name. 
        /// </summary>
        /// <returns>IIIF manifest containing results of specified named query</returns>
        [Route("{customer}/{namedQueryName}/{**namedQueryArgs}")]
        [HttpGet]
        public Task<IActionResult> Index(string customer, string namedQueryName, string? namedQueryArgs = null,
            CancellationToken cancellationToken = default)
            => GenerateIIIFDescriptionResource(() => new GetNamedQueryResults(customer, namedQueryName, namedQueryArgs),
                cancellationToken);
    }
}