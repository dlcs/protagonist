using System.Threading.Tasks;
using DLCS.HydraModel;
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
        await mediator.Send(new ReingestImage(){ SpaceId = spaceId, ImageId = imageId });
        return RedirectToPage("/Images/Index", new { space = spaceId, image = imageId });
    }
        
    [HttpPost]
    public async Task<IActionResult> Delete([FromForm] int spaceId, [FromForm] string imageId )
    {
        await mediator.Send(new DeleteImage(){ SpaceId = spaceId, ImageId = imageId });
        return RedirectToPage("/Spaces/Details",new { id = spaceId});
    }

    [HttpPost]
    public async Task<IActionResult> Patch([FromForm] int spaceId, [FromForm] string imageId, 
        [FromForm] string? string1, [FromForm] string? string2, [FromForm] string? string3,
        [FromForm] int number1, [FromForm] int number2, [FromForm] int number3)
    {
        var patchedFields = new Image()
        {
            String1 = string1 ?? string.Empty,
            String2 = string2 ?? string.Empty,
            String3 = string3 ?? string.Empty,
            Number1 = number1,
            Number2 = number2,
            Number3 = number3
        };
        await mediator.Send(new PatchImage(){ Image = patchedFields, SpaceId = spaceId, ImageId = imageId });
        return RedirectToPage("/Images/Index", new { space = spaceId, image = imageId });
    }
}