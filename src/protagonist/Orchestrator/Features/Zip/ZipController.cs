using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Features.Zip.Requests;
using Orchestrator.Infrastructure;
using Orchestrator.Settings;

namespace Orchestrator.Features.Zip;

[ApiController]
public class ZipController : PersistedNamedQueryControllerBase
{
    public ZipController(
        IMediator mediator,
        IOptions<NamedQuerySettings> namedQuerySettings,
        IOptions<CacheSettings> cacheSettings,
        ILogger<ZipController> logger) : base(mediator, namedQuerySettings, cacheSettings, logger)
    {
    }

    /// <summary>
    /// Get results of named query with specified name. This is proejcted into a zip containing all image results.
    /// </summary>
    /// <returns>Zip archive containing iamges from specified named query</returns>
    [Route("zip/{customer}/{namedQueryName}/{**namedQueryArgs}")]
    [HttpGet]
    public Task<IActionResult> GetPdf(string customer, string namedQueryName, string? namedQueryArgs = null,
        CancellationToken cancellationToken = default)
        => GetProjection(() => new GetZipFromNamedQuery(customer, namedQueryName, namedQueryArgs),
            "application/zip", cancellationToken);
    
    /// <summary>
    /// Get ZIP control file for named query with specified name and args
    /// </summary>
    /// <returns>Zip control-file for results of specified named query</returns>
    [Route("zip-control/{customer}/{namedQueryName}/{**namedQueryArgs}")]
    [HttpGet]
    public Task<IActionResult> GetControlFile(string customer, string namedQueryName,
        string? namedQueryArgs = null, CancellationToken cancellationToken = default)
        => GetControlFile(() => new GetZipControlFileForNamedQuery(customer, namedQueryName, namedQueryArgs),
            cancellationToken);
}