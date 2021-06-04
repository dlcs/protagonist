using System.Security.Claims;
using System.Threading.Tasks;
using API.JsonLd;
using DLCS.Core.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Portal.Features.Spaces.Models;
using Portal.Features.Spaces.Requests;

namespace Portal.Pages.Spaces
{
    [BindProperties]
    public class Details : PageModel
    {
        private readonly IMediator mediator;
        
        public DlcsSettings DlcsSettings { get; }
        
        public SpacePageModel SpacePageModel { get; set; }
        
        public string Customer { get; set; }
        
        public string Space { get; set; }

        public Details(
            IMediator mediator,
            IOptions<DlcsSettings> dlcsSettings,
            ClaimsPrincipal currentUser)
        {
            this.mediator = mediator;
            DlcsSettings = dlcsSettings.Value;
            this.Customer = (currentUser.GetCustomerId() ?? -1).ToString();
        }
        
        public async Task<IActionResult> OnGetAsync(int id)
        {
            Space = id.ToString();
            SpacePageModel = await mediator.Send(new GetSpaceDetails(id, nameof(Image.Number1)));

            if (SpacePageModel.Space == null)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostConvert(int spaceId, bool manifestMode)
        {
            // TODO - handle failure
            var result = await mediator.Send(new ToggleManifestMode(spaceId, manifestMode));

            return RedirectToPage("/spaces/details", new {id = spaceId});
        }
    }
}