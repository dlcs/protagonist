using System.Threading.Tasks;
using API.Client;
using DLCS.Core.Enum;
using DLCS.HydraModel;
using Hydra.Collections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.ViewComponents;

namespace Portal.Pages.Queue;

public class IndexModel : PageModel
{
    private readonly IDlcsClient dlcsClient;
    private const string ActiveQueueName = "active";
    private const string RecentQueueName = "recent";
    public HydraCollection<Batch> Batches { get; set; }
    public CustomerQueue Queue { get; set; }
    public BatchQueueType QueueType { get; set; }
    public PagerValues? PagerValues { get; private set; }
    
    public IndexModel(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }

    public async Task OnGetAsync(
        [FromRoute] string queueType, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = PagerViewComponent.DefaultPageSize)
    {
        Queue = await dlcsClient.GetQueue();
        
        switch (queueType)
        {
            case ActiveQueueName:
                Batches = await dlcsClient.GetBatches(ActiveQueueName, page, pageSize);    
                QueueType = BatchQueueType.Active;
                break;
            default:
                Batches = await dlcsClient.GetBatches(RecentQueueName, page, pageSize);   
                QueueType = BatchQueueType.Recent;
                break;
        }
        
        PagerValues = new PagerValues(Batches.TotalItems, page, pageSize, null, true);
    }
    
    public enum BatchQueueType
    {
        Recent,
        Active
    }
}