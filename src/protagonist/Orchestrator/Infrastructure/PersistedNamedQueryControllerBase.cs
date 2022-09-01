using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure;

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
        Mediator = mediator;
        Logger = logger;
        NamedQuerySettings = namedQuerySettings.Value;
        CacheSettings = cacheSettings.Value;
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
        return await this.HandleAssetRequest(async () =>
        {
            var request = generateRequest();
            var result = await Mediator.Send(request, cancellationToken);

            // Handle known non-200 status
            if (result.IsBadRequest) return BadRequest();
            if (result.Status == PersistedProjectionStatus.Restricted) return Unauthorized();
            if (result.Status == PersistedProjectionStatus.Error) return StatusCode(500);
            if (result.Status == PersistedProjectionStatus.InProcess) return this.InProcess(NamedQuerySettings.ControlStaleSecs);
            if (result.IsEmpty) return NotFound();

            SetCacheControl(result.RequiresAuth);
            return File(result.DataStream, contentType);
        }, Logger);
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
        return await this.HandleAssetRequest(async () =>
        {
            var request = generateRequest();
            var result = await Mediator.Send(request, cancellationToken);

            if (result == null) return NotFound();

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }, Logger);
    }

    private void SetCacheControl(bool requiresAuth)
    {
        var maxAge = TimeSpan.FromSeconds(CacheSettings.GetTtl(CacheDuration.Default, CacheSource.Http));
        this.SetCacheControl(requiresAuth, maxAge);
    }
}