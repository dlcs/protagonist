using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orchestrator.Features.Files.Requests;

namespace Orchestrator.Features.Files;

[Route("[controller]/{customerId}/{spaceId}/")]
[ApiController]
public class FileController : Controller
{
    private readonly IMediator mediator;
    private readonly ILogger<FileController> logger;

    public FileController(IMediator mediator, ILogger<FileController> logger)
    {
        this.mediator = mediator;
        this.logger = logger;
    }

    /// <summary>
    /// Get specified file
    /// </summary>
    [Route("{fileId}")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var response = await mediator.Send(new GetFile(HttpContext.Request.Path), cancellationToken);

            if (response.IsEmpty)
            {
                return NotFound();
            }

            return File(response.Stream, response.ContentType ?? "application/octet-stream");
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
}