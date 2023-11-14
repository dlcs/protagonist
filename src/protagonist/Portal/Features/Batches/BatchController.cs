using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Portal.Features.Batches.Requests;

namespace Portal.Features.Batches;

[Route("[controller]/[action]")]
public class BatchController : Controller
{
    private readonly IMediator mediator;

    public BatchController(IMediator mediator)
    {
        this.mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Test(int batch)
    {
        var response = await mediator.Send(new TestBatch(){ BatchId = batch});
        return Ok(response);
    }
}