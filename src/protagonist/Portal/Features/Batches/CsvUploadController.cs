using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Client;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Portal.Features.Batches.Requests;

namespace Portal.Features.Batches;

public class CsvUploadController : Controller
{
    private readonly ClaimsPrincipal currentUser;
    private readonly IMediator mediator;
    private readonly IDlcsClient dlcsClient;

    public CsvUploadController(IMediator mediator, IDlcsClient dlcsClient, ClaimsPrincipal currentUser)
    {
        this.mediator = mediator;
        this.dlcsClient = dlcsClient;
        this.currentUser = currentUser;
    }

    [HttpPost]
    [Route("[controller]/{customer}/{space}/[action]")]
    public async Task<IActionResult> Upload(int customer, int space, List<IFormFile> file)
    {
        var request = await mediator.Send(new ParseCsv(){ File = file[0]});
        
        if (!request.IsSuccess)
        {
            return BadRequest(request);
        }
        
        return Ok();
    }
}