using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Features.Queue.Request;
using Portal.ViewComponents;

namespace Portal.Pages.Queue;

public class IndexModel : PageModel
{
    private readonly IMediator mediator;
    private const string ActiveQueueName = "active";
    public HydraCollection<Batch> Batches { get; set; }
    public CustomerQueue? Queue { get; set; }
    public QueueType QueueType { get; set; }
    public PagerValues? PagerValues { get; private set; }
    
    public IndexModel(IDlcsClient dlcsClient, IMediator mediator)
    {
        this.mediator = mediator;
    }

    public async Task OnGetAsync(
        [FromRoute] string queueType, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = PagerViewComponent.DefaultPageSize)
    {
        QueueType = queueType == ActiveQueueName ? QueueType.Active : QueueType.Recent;
        
        var queueResult = await mediator.Send(new GetQueue(){ Type = QueueType, Page = page, PageSize = pageSize });
        
        Queue = queueResult.Queue;
        Batches = queueResult.Batches;
        PagerValues = new PagerValues(Batches.TotalItems, page, pageSize, null, true);
    }
}