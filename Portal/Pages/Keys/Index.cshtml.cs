using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Keys.Requests;

namespace Portal.Pages.Keys
{
    public class IndexModel : PageModel
    {
        private readonly IMediator mediator;
        
        [BindProperty]
        public IEnumerable<string> ApiKeys { get; set; } = Enumerable.Empty<string>();

        public IndexModel(IMediator mediator)
        {
            this.mediator = mediator;
        }
        
        public async Task OnGetAsync()
        {
            ApiKeys = await mediator.Send(new GetCustomerApiKeys()) ?? Enumerable.Empty<string>();
        }
    }
}