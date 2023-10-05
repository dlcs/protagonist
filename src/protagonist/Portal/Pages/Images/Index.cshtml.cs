using System.Security.Claims;
using System.Threading.Tasks;
using DLCS.Core.Settings;
using DLCS.HydraModel;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Portal.Features.Spaces.Requests;

namespace Portal.Pages.Images;

[BindProperties]
public class Index : PageModel
{
    private readonly IMediator mediator;
    
    public DlcsSettings DlcsSettings { get; }

    public Image Image { get; set; }
    
    public Index(
        IMediator mediator,
        IOptions<DlcsSettings> dlcsSettings,
        ClaimsPrincipal currentUser)
    {
        this.mediator = mediator;
        DlcsSettings = dlcsSettings.Value;
    }

    public async Task<IActionResult> OnGetAsync(int space, string image)
    {
        Image = await mediator.Send(new GetImage {SpaceId = space, ImageId = image});
        return Page();
    }
}