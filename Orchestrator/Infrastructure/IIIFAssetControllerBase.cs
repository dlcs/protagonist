using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Caching;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Orchestrator.Infrastructure.Mediatr;
using Orchestrator.Models;

namespace Orchestrator.Infrastructure
{
    /// <summary>
    /// Base class for controllers that generate <see cref="DescriptionResourceResponse"/> from <see cref="IAssetRequest"/> request 
    /// </summary>
    public abstract class IIIFAssetControllerBase : Controller
    {
        protected readonly IMediator mediator;
        protected readonly ILogger<IIIFAssetControllerBase> logger;
        protected readonly CacheSettings cacheSettings;

        protected IIIFAssetControllerBase(
            IMediator mediator,
            IOptions<CacheSettings> cacheSettings,
            ILogger<IIIFAssetControllerBase> logger
        )
        {
            this.mediator = mediator;
            this.logger = logger;
            this.cacheSettings = cacheSettings.Value;
        }

        /// <summary>
        /// Generate <see cref="DescriptionResourceResponse"/> from <see cref="IAssetRequest"/> request. Handles known issues
        /// parsing asset request and sets appropriate headers on response.
        /// </summary>
        /// <param name="generateRequest">Function to generate mediatr request.</param>
        /// <param name="cancellationToken">Current cancellation token.</param>
        /// <typeparam name="T">
        /// Type of mediatr request, must be <see cref="IAssetRequest"/> and return <see cref="DescriptionResourceResponse"/>
        /// </typeparam>
        /// <returns>IActionResult, will be NotFoundResult ,BadRequestResult or ContentResult if successful</returns>
        protected async Task<IActionResult> GenerateIIIFDescriptionResource<T>(Func<T> generateRequest,
            CancellationToken cancellationToken = default)
            where T : IRequest<DescriptionResourceResponse>, IAssetRequest
        {
            try
            {
                var request = generateRequest();
                var infoJsonResponse = await mediator.Send(request, cancellationToken);
                if (!infoJsonResponse.HasInfoJson) return NotFound();

                if (infoJsonResponse.IsUnauthorised)
                {
                    Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                }

                SetCacheControl(infoJsonResponse.RequiresAuth);
                HttpContext.Response.Headers[HeaderNames.Vary] = new[] { "Accept-Encoding" };
                return Content(infoJsonResponse.DescriptionResource, "application/json");
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

        private void SetCacheControl(bool requiresAuth)
        {
            var maxAge = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Default, CacheSource.Http));
            HttpContext.Response.GetTypedHeaders().CacheControl =
                new CacheControlHeaderValue
                {
                    Public = !requiresAuth,
                    Private = requiresAuth,
                    MaxAge = maxAge,
                    SharedMaxAge = requiresAuth ? null : maxAge
                };
        }
    }
}