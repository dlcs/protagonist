using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Caching;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Features.NamedQuery.Requests;
using Orchestrator.Infrastructure;

namespace Orchestrator.Features.NamedQuery
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
        [Route("{customer}/{namedQuery}/{**catchAll}")]
        [HttpGet]
        public Task<IActionResult> Index(string customer, string namedQuery, string? catchAll = null,
            CancellationToken cancellationToken = default)
            => GenerateIIIFDescriptionResource(() => new GetNamedQueryResults(customer, namedQuery, catchAll),
                cancellationToken);
    }
}