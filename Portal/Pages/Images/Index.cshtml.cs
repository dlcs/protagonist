using System.Security.Claims;
using System.Threading.Tasks;
using API.JsonLd;
using DLCS.Core.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Portal.Features.Spaces.Requests;

namespace Portal.Pages.Images
{
    public class Index : PageModel
    {
        private readonly IMediator mediator;
        private readonly ClaimsPrincipal currentUser;
        
        [BindProperty]
        public DlcsSettings DlcsSettings { get; }
        
        [BindProperty]
        public string Customer { get; set; }
        
        public Image Image { get; set; }
        
        public Index(
            IMediator mediator,
        IOptions<DlcsSettings> dlcsSettings,
            ClaimsPrincipal currentUser)
        {
            this.mediator = mediator;
            DlcsSettings = dlcsSettings.Value;
            this.currentUser = currentUser;
            this.Customer = (currentUser.GetCustomerId() ?? -1).ToString();
        }

        public async Task<IActionResult> OnGetAsync(int space, string id)
        {
            Image = await this.mediator.Send(new GetImage {SpaceId = space, ImageId = id});

            return Page();
        }
    }
}