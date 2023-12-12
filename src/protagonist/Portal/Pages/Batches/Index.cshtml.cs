using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.HydraModel;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Batches.Requests;
using Portal.ViewComponents;

namespace Portal.Pages.Batches;

[BindProperties]
public class Index : PageModel
{
    private readonly IMediator mediator;
    public Batch Batch { get; set; }
    public HydraCollection<Image> Images { get; set; }
    public Dictionary<string, string> Thumbnails { get; set; }
    public PagerValues? PagerValues { get; private set; }
    
    public Index(IMediator mediator)
    {
        this.mediator = mediator;
    }
    
    public async Task<IActionResult> OnGetAsync(
        [FromRoute] int batch,
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = PagerViewComponent.DefaultPageSize)
    {
        var batchResult = await mediator.Send(new GetBatch{ BatchId = batch, Page = page, PageSize = pageSize});
        if (batchResult == null)
        {
            TempData[PageConstants.TempErrorMessageKey] = "The requested batch was not found";
            return NotFound();
        }
        
        Batch = batchResult.Batch;
        Images = batchResult.Images;
        Thumbnails = batchResult.Thumbnails;
        PagerValues = new PagerValues(Images.TotalItems, page, pageSize, null, false);
        return Page();
    }
    
    public string GetImageReference(Image image)
    {
        return $"{image.String1}/{image.String2}/{image.String3}/{image.Number1}/{image.Number2}/{image.Number3}";
    }
}
