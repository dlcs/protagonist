using System.Collections.Generic;
using System.Threading.Tasks;
using API.Client;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Portal.Features.Batches.Requests;

namespace Portal.Features.Batches;

[Route("[controller]/[action]")]
public class CsvUploadController : Controller
{
    private readonly IMediator mediator;

    public CsvUploadController(IMediator mediator)
    {
        this.mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Upload(List<IFormFile> file, int? space)
    {
        var request = await mediator.Send(new IngestFromCsv(){ SpaceId = space, File = file[0]});
        
        if (!request.IsSuccess)
        {
            return BadRequest(request);
        }
        
        return Ok();
    }
}