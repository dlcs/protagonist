using System.Security.Claims;
using System.Threading.Tasks;
using API.JsonLd;
using DLCS.Core.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Portal.Features.Spaces.Models;
using Portal.Features.Spaces.Requests;

namespace Portal.Pages.Spaces
{
    public class Details : PageModel
    {
        private readonly IMediator mediator;
        private readonly ClaimsPrincipal currentUser;
        
        [BindProperty]
        public DlcsSettings DlcsSettings { get; }
        
        [BindProperty]
        public SpacePageModel SpacePageModel { get; set; }
        
        [BindProperty]
        public string Customer { get; set; }
        
        [BindProperty]
        public string Space { get; set; }

        public Details(
            IMediator mediator,
            IOptions<DlcsSettings> dlcsSettings,
            ClaimsPrincipal currentUser)
        {
            this.mediator = mediator;
            DlcsSettings = dlcsSettings.Value;
            this.currentUser = currentUser;
            this.Customer = (currentUser.GetCustomerId() ?? -1).ToString();
        }
        
        public async Task<IActionResult> OnGetAsync(int id)
        {
            Space = id.ToString();
            SpacePageModel = await this.mediator.Send(new GetSpaceDetails {SpaceId = id});

            if (SpacePageModel.Space == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}