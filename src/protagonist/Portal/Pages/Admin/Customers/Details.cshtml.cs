using System.Collections.Generic;
using System.Threading.Tasks;
using API.Client;
using DLCS.Model.Customers;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Admin.Requests;
using Customer = DLCS.Model.Customers.Customer;

namespace Portal.Pages.Admin.Customers;

public class Details : PageModel
{
    private readonly IMediator mediator;
    private readonly IDlcsClient dlcsClient;
    
    public Details(
        IMediator mediator,
        IDlcsClient dlcsClient)
    {
        this.mediator = mediator;
        this.dlcsClient = dlcsClient;
    }
    
    public Customer Customer { get; set; }
    public IEnumerable<User> Users { get; set; }
    public HydraCollection<DLCS.HydraModel.Space> PageOfSpaces { get; set; }
    
    public async Task<IActionResult> OnGetAsync(int id)
    {
        Customer = await mediator.Send(new GetCustomer(id)); // TODO -> API
        PageOfSpaces = await dlcsClient.GetSpaces(1, 10, null, true, Customer.Id);
        Users = await mediator.Send(new GetUsers(Customer.Id)); // TODO -> API
        return Page();
    }
}