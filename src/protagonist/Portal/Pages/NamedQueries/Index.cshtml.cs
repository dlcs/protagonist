using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.HydraModel;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.NamedQueries.Requests;

namespace Portal.Pages.NamedQueries;

public class IndexModel : PageModel
{
    private readonly IMediator mediator;
    
    [BindProperty]
    public IEnumerable<NamedQuery> NamedQueries { get; set; } = Enumerable.Empty<NamedQuery>();
    
    public bool? Success;
    
    public IndexModel(IMediator mediator)
    {
        this.mediator = mediator;
    }
    
    public async Task OnGetAsync([FromQuery] bool? success = null)
    {
        Success = success;
        NamedQueries = await mediator.Send(new GetCustomerNamedQueries());
    }
}