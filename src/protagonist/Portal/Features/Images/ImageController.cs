using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Portal.Features.Images.Requests;

namespace Portal.Features.Images;

[Route("[controller]/[action]")]
public class ImageController : Controller
{
    private readonly IMediator mediator;

    public ImageController(IMediator mediator)
    {
        this.mediator = mediator;
    }
    
    [HttpPost]
    public async Task<IActionResult> Reingest([FromForm] int spaceId, [FromForm] string imageId )
    {
        await mediator.Send(new ReingestImage(){SpaceId = spaceId, ImageId = imageId});
        return RedirectToPage("/Images/Index", new { space = spaceId, image = imageId });
    }
        
    [HttpPost]
    public async Task<IActionResult> Delete([FromForm] int spaceId, [FromForm] string imageId )
    {
        await mediator.Send(new DeleteImage(){SpaceId = spaceId, ImageId = imageId});
        return RedirectToPage("/Spaces/Index");
    }
}