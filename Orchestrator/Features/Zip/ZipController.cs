using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Orchestrator.Features.Zip
{
    [ApiController]
    public class ZipController
    {
        /// <summary>
        /// Get results of named query with specified name. This is proejcted into a zip containing all image results.
        /// </summary>
        /// <returns>PDF containing results of specified named query</returns>
        [Route("zip/{customer}/{namedQueryName}/{**namedQueryArgs}")]
        [HttpGet]
        public async Task<IActionResult> GetPdf(string customer, string namedQueryName, string? namedQueryArgs = null,
            CancellationToken cancellationToken = default)
        {
            
        }
    }
}