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
        await mediator.Send(new UpdateNamedQuery(){ NamedQueryId = namedQueryId, Template = template });
        return RedirectToPage("/NamedQueries/Index");
    }
    
    [HttpPost]
    public async Task<IActionResult> Create([FromForm] string queryName, [FromForm] string template)
    {
        try
        {
            await mediator.Send(new CreateNamedQuery() { Name = queryName, Template = template });
            return RedirectToPage("/NamedQueries/Index", new { success = true });
        }
        catch (DlcsException)
        {
            return RedirectToPage("/NamedQueries/Index",new { success = false });
        }
    }
}