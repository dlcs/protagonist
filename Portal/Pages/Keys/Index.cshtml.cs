using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Keys;

namespace Portal.Pages.Keys
{
    public class Index : PageModel
    {
        private readonly IMediator mediator;
        
        [BindProperty]
        public IEnumerable<string> ApiKeys { get; set; } = Enumerable.Empty<string>();

        public Index(IMediator mediator)
        {
            this.mediator = mediator;
        }
        
        public async Task OnGetAsync()
        {
            ApiKeys = await mediator.Send(new GetCustomerApiKeys()) ?? Enumerable.Empty<string>();
        }
    }
}