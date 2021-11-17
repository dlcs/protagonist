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
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure
{
    /// <summary>
    /// Base class for controllers that return persisted NamedQuery resources.
    /// </summary>
    public class PersistedNamedQueryControllerBase : Controller
    {
        protected readonly IMediator Mediator;
        protected readonly ILogger Logger;
        protected readonly NamedQuerySettings NamedQuerySettings;
        protected readonly CacheSettings CacheSettings;
        
        public PersistedNamedQueryControllerBase(
            IMediator mediator, 
            IOptions<NamedQuerySettings> namedQuerySettings,
            IOptions<CacheSettings> cacheSettings,
            ILogger logger)
        {
            this.Mediator = mediator;
            this.Logger = logger;
            this.NamedQuerySettings = namedQuerySettings.Value;
            this.CacheSettings = cacheSettings.Value;
        }

        /// <summary>
        /// Generate <see cref="PersistedNamedQueryProjection"/> from request. Handles known issues parsing asset request
        /// and sets appropriate response.
        /// </summary>
        /// <param name="generateRequest">Function to generate mediatr request.</param>
        /// <param name="contentType">Content-type header, used for successful response</param>
        /// <param name="cancellationToken">Current cancellation token.</param>
        /// <typeparam name="T">
        /// Type of mediatr request, must return <see cref="PersistedNamedQueryProjection"/>
        /// </typeparam>
        /// <returns>
        /// IActionResult, will be NotFoundResult, BadRequestResult, 500 StatusCodeResult or FileResult if  successful
        /// </returns>
        protected async Task<IActionResult> GetProjection<T>(Func<T> generateRequest,
            string contentType, CancellationToken cancellationToken = default)
            where T : IRequest<PersistedNamedQueryProjection>
        {
            try
            {
                var request = generateRequest();
                var result = await Mediator.Send(request, cancellationToken);

                // Handle known non-200 status
                if (result.IsBadRequest) return BadRequest();
                if (result.Status == PersistedProjectionStatus.Error) return StatusCode(500);
                if (result.Status == PersistedProjectionStatus.InProcess)
                    return InProcess(NamedQuerySettings.PdfControlStaleSecs);
                if (result.IsEmpty) return NotFound();

                SetCacheControl();
                return File(result.DataStream, contentType);
            }
            catch (KeyNotFoundException ex)
            {
                // TODO - this error handling duplicates same in RequestHandlerBase
                Logger.LogError(ex, "Could not find Customer/Space from '{Path}'", HttpContext.Request.Path);
                return NotFound();
            }
            catch (FormatException ex)
            {
                Logger.LogError(ex, "Error parsing path '{Path}'", HttpContext.Request.Path);
                return BadRequest();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unknown exception returning projection '{Path}'", HttpContext.Request.Path);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Generate ControlFile JSON from request.
        /// </summary>
        /// <param name="generateRequest">Function to generate mediatr request.</param>
        /// <param name="cancellationToken">Current cancellation token.</param>
        /// <typeparam name="T">Type of response from mediatr request</typeparam>
        /// <returns>Relevant IActionResult, depending on success or failure</returns>
        protected async Task<IActionResult> GetControlFile<T>(Func<IRequest<T>> generateRequest,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var request = generateRequest();
                var result = await Mediator.Send(request, cancellationToken);

                if (result == null) return NotFound();

                return Content(JsonConvert.SerializeObject(result), "application/json");
            }
            catch (KeyNotFoundException ex)
            {
                // TODO - this error handling duplicates same in RequestHandlerBase
                Logger.LogError(ex, "Could not find Customer/Space from '{Path}'", HttpContext.Request.Path);
                return NotFound();
            }
            catch (FormatException ex)
            {
                Logger.LogError(ex, "Error parsing path '{Path}'", HttpContext.Request.Path);
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
            var maxAge = TimeSpan.FromSeconds(CacheSettings.GetTtl(CacheDuration.Default, CacheSource.Http));
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