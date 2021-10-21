using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Caching;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Orchestrator.Features.PDF.Requests;
using Orchestrator.Settings;

namespace Orchestrator.Features.PDF
{
    [ApiController]
    public class PdfController : Controller
    {
        private readonly IMediator mediator;
        private readonly ILogger<PdfController> logger;
        private readonly NamedQuerySettings namedQuerySettings;
        private readonly CacheSettings cacheSettings;

        public PdfController(
            IMediator mediator, 
            IOptions<NamedQuerySettings> namedQuerySettings,
            IOptions<CacheSettings> cacheSettings,
            ILogger<PdfController> logger)
        {
            this.mediator = mediator;
            this.logger = logger;
            this.namedQuerySettings = namedQuerySettings.Value;
            this.cacheSettings = cacheSettings.Value;
        }
        
        /// <summary>
        /// Get results of named query with specified name. This is transformed into a PDF containing all image results.
        /// </summary>
        /// <returns>PDF containing results of specified named query</returns>
        [Route("pdf/{customer}/{namedQueryName}/{**namedQueryArgs}")]
        [HttpGet]
        public async Task<IActionResult> GetPdf(string customer, string namedQueryName, string? namedQueryArgs = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new GetPdfFromNamedQuery(customer, namedQueryName, namedQueryArgs);
                var result = await mediator.Send(request, cancellationToken);

                // Handle known non-200 status
                if (result.IsBadRequest) return BadRequest();
                if (result.Status == PdfStatus.Error) return StatusCode(500);
                if (result.Status == PdfStatus.InProcess) return InProcess(namedQuerySettings.PdfControlStaleSecs);
                if (result.IsEmpty) return NotFound();

                SetCacheControl();
                return File(result.PdfStream, "application/pdf");
            }
            catch (KeyNotFoundException ex)
            {
                // TODO - this error handling duplicates same in RequestHandlerBase
                logger.LogError(ex, "Could not find Customer/Space from '{Path}'", HttpContext.Request.Path);
                return NotFound();
            }
            catch (FormatException ex)
            {
                logger.LogError(ex, "Error parsing path '{Path}'", HttpContext.Request.Path);
                return BadRequest();
            }
        }

        /// <summary>
        /// Get PDF control file for named query with specified name and args
        /// </summary>
        /// <returns>PDF control-file for results of specified named query</returns>
        [Route("pdf-control/{customer}/{namedQueryName}/{**namedQueryArgs}")]
        [HttpGet]
        public async Task<IActionResult> GetControlFile(string customer, string namedQueryName,
            string? namedQueryArgs = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new GetPdfControlFileForNamedQuery(customer, namedQueryName, namedQueryArgs);
                var result = await mediator.Send(request, cancellationToken);
                
                if (result == null) return NotFound();

                return Content(JsonConvert.SerializeObject(result), "application/json");
            }
            catch (KeyNotFoundException ex)
            {
                // TODO - this error handling duplicates same in RequestHandlerBase
                logger.LogError(ex, "Could not find Customer/Space from '{Path}'", HttpContext.Request.Path);
                return NotFound();
            }
            catch (FormatException ex)
            {
                logger.LogError(ex, "Error parsing path '{Path}'", HttpContext.Request.Path);
                return BadRequest();
            }
        }

        private IActionResult InProcess(int retryAfter)
        {
            Response.Headers.Add("Retry-After", retryAfter.ToString());
            return new StatusCodeResult(202);
        }
        
        private void SetCacheControl()
        {
            var maxAge = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Default, CacheSource.Http));
            HttpContext.Response.GetTypedHeaders().CacheControl =
                new CacheControlHeaderValue
                {
                    Public = true,
                    MaxAge = maxAge,
                    SharedMaxAge = maxAge
                };
        }
    }
}