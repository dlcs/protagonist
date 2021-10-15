using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Orchestrator.Features.PDF
{
    [Route("pdf")]
    [ApiController]
    public class PdfController : Controller
    {
        /// <summary>
        /// Get results of named query with specified name. This is transformed into a PDF containing all image results.
        /// </summary>
        /// <returns>PDF containing results of specified named query</returns>
        [Route("{customer}/{namedQueryName}/{**namedQueryArgs}")]
        [HttpGet]
        public async Task<IActionResult> Index(string customer, string namedQueryName, string? namedQueryArgs = null,
            CancellationToken cancellationToken = default)
        {
            return new ObjectResult(new
            {
                customer, namedQueryName, namedQueryArgs
            });
        }
    }
}