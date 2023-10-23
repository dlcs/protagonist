using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.HydraModel;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Batches.Requests;
using Portal.Features.Spaces.Requests;


namespace Portal.Pages.Batches;

[BindProperties]
public class Index : PageModel
{
    private readonly IMediator mediator;
    public Batch Batch { get; set; }
    public HydraCollection<Image> Images { get; set; }
    public Dictionary<string, string> Thumbnails { get; set; }
    
    public Index(IMediator mediator)
    {
        this.mediator = mediator;
    }
    
    public async Task<IActionResult> OnGetAsync(int batch)
    {
        var batchResult = await mediator.Send(new GetBatch{ BatchId = batch });
        Batch = batchResult.Batch;
        Images = batchResult.Images;
        Thumbnails = batchResult.Thumbnails;
        return Page();
    }
}
