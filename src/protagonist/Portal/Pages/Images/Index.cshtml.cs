using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DLCS.Core.Settings;
using DLCS.HydraModel;
using IIIF;
using IIIF.ImageApi.V3;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Portal.Features.Images.Requests;
using Portal.Features.Spaces.Requests;

namespace Portal.Pages.Images;

[BindProperties]
public class Index : PageModel
{
    private readonly IMediator mediator;
    public Image Image { get; set; }
    public ImageService3? Thumbnails { get; set; }
    
    public Index(IMediator mediator, ClaimsPrincipal currentUser)
    {
        this.mediator = mediator;
    }

    public async Task<IActionResult> OnGetAsync(int space, string image)
    {
        var imageResult = await mediator.Send(new GetImage{SpaceId = space, ImageId = image});
        Image = imageResult.Image;
        Thumbnails = imageResult.ImageService;
        return Page();
    }
    
    public string CreateSrc(Size size)
    {
        return $"{Image.ThumbnailImageService}/full/{size.Width},{size.Height}/0/default.jpg";
    }
}