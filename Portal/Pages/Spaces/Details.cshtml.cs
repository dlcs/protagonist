using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json.Linq;
using Portal.Features.Spaces.Requests;

namespace Portal.Pages.Spaces
{
    public class Details : PageModel
    {
        private readonly IMediator mediator;
        
        [BindProperty]
        public JObject Space { get; set; }

        public Details(IMediator mediator)
        {
            this.mediator = mediator;
        }
        
        public async Task<IActionResult> OnGetAsync(int id)
        {
            Space = await this.mediator.Send(new GetSpaceDetails {SpaceId = id});

            if (Space == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}