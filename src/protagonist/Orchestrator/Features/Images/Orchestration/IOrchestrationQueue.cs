using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Orchestrator.Assets;

namespace Orchestrator.Features.Images.Orchestration;

/// <summary>
/// Interface for operations related to queueing/dequeueing asynchronous orchestration requests.
/// </summary>
public interface IOrchestrationQueue
{
    /// <summary>
    /// Queue orchestration request.
    /// </summary>
    ValueTask QueueRequest(OrchestrationImage orchestrationImage, CancellationToken cancellationToken);

    /// <summary>
    /// Get next waiting image to be orchestrated.
    /// </summary>
    ValueTask<OrchestrationImage> DequeueRequest(CancellationToken cancellationToken);
}

/// <summary>
/// Implementation of <see cref="IOrchestrationQueue"/> using a bounded channel for read/writing
/// </summary>
public class BoundedChannelOrchestrationQueue : IOrchestrationQueue
{
    private readonly Channel<OrchestrationImage> queue;

    public BoundedChannelOrchestrationQueue(int capacity)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        };
        queue = Channel.CreateBounded<OrchestrationImage>(options);
    }

    public ValueTask QueueRequest(OrchestrationImage orchestrationImage, CancellationToken cancellationToken)
        => queue.Writer.WriteAsync(orchestrationImage, cancellationToken);

    public ValueTask<OrchestrationImage> DequeueRequest(CancellationToken cancellationToken)
        => queue.Reader.ReadAsync(cancellationToken);
}