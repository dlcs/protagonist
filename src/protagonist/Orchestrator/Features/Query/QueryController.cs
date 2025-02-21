using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Collections;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Orchestrator.Features.Query.Requests;
using Orchestrator.Infrastructure;

namespace Orchestrator.Features.Query;

[Route("raw-resource")]
[ApiController]
public class QueryController : Controller
{
    private readonly IMediator mediator;
    private readonly CacheSettings cacheSettings;

    public QueryController(IMediator mediator, IOptions<CacheSettings> cacheSettings)
    {
        this.mediator = mediator;
        this.cacheSettings = cacheSettings.Value;
    }
    
    /// <summary>
    /// Get results of asset ids matching named query
    /// </summary>
    /// <returns>Matching AssetIds for results of specified named query</returns>
    [Route("{customer}/{namedQueryName}/{**namedQueryArgs}")]
    [HttpGet]
    public async Task<IActionResult> Index(string customer, string namedQueryName, string? namedQueryArgs = null,
        CancellationToken cancellationToken = default)
    {
        var request = new GetNamedQueryAssetIds(customer, namedQueryName, namedQueryArgs);
        var results = await mediator.Send(request, cancellationToken);

        if (results.Success && !results.Value.IsNullOrEmpty())
        {
            SetCacheControlHeaders();
            return Ok(results.Value.Select(v => v.ToString()));
        }

        if ((results.ErrorCode ?? 0) == 400) return BadRequest();
        
        return NotFound();
    }
    
    private void SetCacheControlHeaders()
    {
        var maxAge = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Default, CacheSource.Http));
        this.SetCacheControl(false, maxAge);
    }
}

