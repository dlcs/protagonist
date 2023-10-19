using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using Hydra.Collections;
using MediatR;

namespace Portal.Features.Queue.Request;

public class GetQueue : IRequest<GetQueueResult>
{
    public QueueType Type { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class GetQueueResult
{
    public CustomerQueue? Queue { get; set; }
    public HydraCollection<Batch> Batches { get; set; }
}

public enum QueueType
{
    Recent,
    Active
}

public class GetQueueHandler : IRequestHandler<GetQueue, GetQueueResult>
{
    private readonly IDlcsClient dlcsClient;

    public GetQueueHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }
    
    public async Task<GetQueueResult> Handle(GetQueue request, CancellationToken cancellationToken)
    {
        if (request.Type == QueueType.Active)
        {
            var batches = await dlcsClient.GetBatches("active", request.Page, request.PageSize);
            var queue = await dlcsClient.GetQueue();
            
            return new GetQueueResult()
            {
                Queue = queue,
                Batches = batches
            };
        }
        else
        {
            var batches = await dlcsClient.GetBatches("recent", request.Page, request.PageSize);
            
            return new GetQueueResult()
            {
                Batches = batches
            };
        }
    }
}