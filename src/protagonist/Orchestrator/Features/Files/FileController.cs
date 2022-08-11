using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orchestrator.Features.Files.Requests;
using Orchestrator.Infrastructure;

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
        return await this.HandleAssetRequest(async () =>
        {
            var response = await mediator.Send(new GetFile(HttpContext.Request.Path), cancellationToken);

            if (response.IsEmpty)
            {
                return NotFound();
            }

            return File(response.Stream, response.ContentType ?? "application/octet-stream");
        }, logger);
    }
}