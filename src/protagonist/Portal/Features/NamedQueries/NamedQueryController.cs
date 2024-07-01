using System;
using System.Threading.Tasks;
using API.Client;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Portal.Features.NamedQueries.Requests;

namespace Portal.Features.NamedQueries;

[Route("[controller]/[action]")]
public class NamedQueryController : Controller
{
    private readonly IMediator mediator;
    
    public NamedQueryController(IMediator mediator)
    {
        this.mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Delete([FromForm] string namedQueryId)
    {
        await mediator.Send(new DeleteNamedQuery(){ NamedQueryId = namedQueryId });
        return RedirectToPage("/NamedQueries/Index");
    }
    
    [HttpPost]
    public async Task<IActionResult> Update([FromForm] string namedQueryId, [FromForm] string template)
    {
        try
        {
            await mediator.Send(new UpdateNamedQuery(){ NamedQueryId = namedQueryId, Template = template });
            return Ok();
        }
        catch (DlcsException dlcsEx)
        {
            return BadRequest(dlcsEx.Message);
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> Create(string queryName, string queryTemplate)
    {
        try
        {
            await mediator.Send(new CreateNamedQuery() { Name = queryName, Template = queryTemplate });
            return Ok();
        }
        catch (DlcsException dlcsEx)
        {
            return BadRequest(dlcsEx.Message);
        }
    }
}