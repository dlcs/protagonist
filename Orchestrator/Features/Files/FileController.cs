using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.Features.Files.Requests;

namespace Orchestrator.Features.Files
{
    [Route("[controller]/{customerId}/{spaceId}/")]
    [ApiController]
    public class FileController : Controller
    {
        private readonly IMediator mediator;

        public FileController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        /// <summary>
        /// Get specified file
        /// </summary>
        [Route("{fileId}")]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var response = await mediator.Send(new GetFile(HttpContext.Request.Path), cancellationToken);

            if (response.IsEmpty)
            {
                return NotFound();
            }

            return File(response.Stream, response.ContentType ?? "application/octet-stream");
        }
    }
}