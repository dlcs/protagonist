using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Repository.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Admin.Commands;
using Portal.Features.Spaces.Models;
using Portal.Features.Spaces.Requests;
using Customer = DLCS.Model.Customers.Customer;

namespace Portal.Pages.Admin.Customers
{
    public class Details : PageModel
    {
        private readonly IMediator mediator;
        
        public Details(IMediator mediator)
        {
            this.mediator = mediator;
        }
        
        public Customer Customer { get; set; }
        public IEnumerable<User> Users { get; set; }
        public PageOfSpaces PageOfSpaces { get; set; }
        
        public async Task<IActionResult> OnGetAsync(int id)
        {
            Customer = await mediator.Send(new GetCustomer(id));
            PageOfSpaces = await mediator.Send(new GetPageOfSpaces(1, 10, Customer.Id));
            Users = await mediator.Send(new GetUsers(Customer.Id));
            return Page();
        }
    }
}