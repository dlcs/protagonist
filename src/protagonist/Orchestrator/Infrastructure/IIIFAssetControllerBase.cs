using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using IIIF.Serialisation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Orchestrator.Infrastructure.Mediatr;
using Orchestrator.Models;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Base class for controllers that generate <see cref="DescriptionResourceResponse"/> from
/// <see cref="IAssetRequest"/> request 
/// </summary>
public abstract class IIIFAssetControllerBase : Controller
{
    protected readonly IMediator Mediator;
    protected readonly ILogger Logger;
    protected readonly CacheSettings CacheSettings;

    protected IIIFAssetControllerBase(IMediator mediator, IOptions<CacheSettings> cacheSettings, ILogger logger)
    {
        Mediator = mediator;
        Logger = logger;
        CacheSettings = cacheSettings.Value;
    }

    /// <summary>
    /// Generate <see cref="DescriptionResourceResponse"/> from request. Handles known issues parsing asset request
    /// and sets appropriate headers on response.
    /// </summary>
    /// <param name="generateRequest">Function to generate mediatr request.</param>
    /// <param name="contentType">Content-type header, used for successful response</param>
    /// <param name="cacheTtl">Http Cache ttl, if not specified defaults are used</param>
    /// <param name="cancellationToken">Current cancellation token.</param>
    /// <typeparam name="T">
    /// Type of mediatr request, must be <see cref="IAssetRequest"/> and return <see cref="DescriptionResourceResponse"/>
    /// </typeparam>
    /// <returns>IActionResult, will be NotFoundResult ,BadRequestResult or ContentResult if successful</returns>
    protected async Task<IActionResult> GenerateIIIFDescriptionResource<T>(Func<T> generateRequest,
        string contentType = "application/json",
        int? cacheTtl = null,
        CancellationToken cancellationToken = default)
        where T : IRequest<DescriptionResourceResponse>
    {
        return await this.HandleAssetRequest(async () =>
        {
            var request = generateRequest();
            var descriptionResource = await Mediator.Send(request, cancellationToken);

            if (descriptionResource.IsBadRequest) return BadRequest();
            if (!descriptionResource.HasResource) return NotFound();

            if (descriptionResource.IsUnauthorised)
            {
                Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            }

            SetCacheControl(descriptionResource.RequiresAuth, cacheTtl);
            HttpContext.Response.Headers[HeaderNames.Vary] = new[] { "Accept-Encoding", "Accept" };
            return Content(descriptionResource.DescriptionResource!.AsJson(), contentType);
        }, Logger);
    }

    private void SetCacheControl(bool requiresAuth, int? cacheTtl = null)
    {
        var maxAge = TimeSpan.FromSeconds(cacheTtl ?? CacheSettings.GetTtl(CacheDuration.Default, CacheSource.Http));
        this.SetCacheControl(requiresAuth, maxAge);
    }
}