using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Model.Customers;
using MediatR;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Admin.Requests;

namespace Portal.Pages.Admin.Customers;

public class Index : PageModel
{
    private readonly IMediator mediator;

    public Index(IMediator mediator)
    {
        this.mediator = mediator;
    }
    
    public async Task OnGetAsync()
    {
        Customers = await mediator.Send(new GetAllCustomers());
    }

    public IEnumerable<Customer> Customers { get; set; }
}