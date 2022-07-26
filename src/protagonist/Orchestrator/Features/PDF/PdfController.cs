using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Caching;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Features.PDF.Requests;
using Orchestrator.Infrastructure;
using Orchestrator.Settings;

namespace Orchestrator.Features.PDF
{
    [ApiController]
    public class PdfController : PersistedNamedQueryControllerBase
    {
        public PdfController(
            IMediator mediator, 
            IOptions<NamedQuerySettings> namedQuerySettings,
            IOptions<CacheSettings> cacheSettings,
            ILogger<PdfController> logger) : base(mediator, namedQuerySettings, cacheSettings, logger)
        {
        }

        /// <summary>
        /// Get results of named query with specified name. This is transformed into a PDF containing all image results.
        /// </summary>
        /// <returns>PDF containing results of specified named query</returns>
        [Route("pdf/{customer}/{namedQueryName}/{**namedQueryArgs}")]
        [HttpGet]
        public Task<IActionResult> GetPdf(string customer, string namedQueryName, string? namedQueryArgs = null,
            CancellationToken cancellationToken = default)
            => GetProjection(() => new GetPdfFromNamedQuery(customer, namedQueryName, namedQueryArgs),
                "application/pdf",
                cancellationToken);

        /// <summary>
        /// Get PDF control file for named query with specified name and args
        /// </summary>
        /// <returns>PDF control-file for results of specified named query</returns>
        [Route("pdf-control/{customer}/{namedQueryName}/{**namedQueryArgs}")]
        [HttpGet]
        public Task<IActionResult> GetControlFile(string customer, string namedQueryName,
            string? namedQueryArgs = null, CancellationToken cancellationToken = default)
            => GetControlFile(() => new GetPdfControlFileForNamedQuery(customer, namedQueryName, namedQueryArgs),
                cancellationToken);
    }
}