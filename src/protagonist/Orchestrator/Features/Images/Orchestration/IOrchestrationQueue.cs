using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DLCS.Web.Logging;
using LazyCache;
using Orchestrator.Assets;

namespace Orchestrator.Features.Images.Orchestration;

/// <summary>
/// A queued orchestration request, along with the correlation-id of the request that raised it.
/// </summary>
/// <param name="OrchestrationImage">Image to orchestrate.</param>
/// <param name="CorrelationId">
/// Correlation-id of the originating request, if known. Orchestration happens on a background thread so this needs to
/// be carried with the request to keep the resulting log events correlated. Without this we have empty correlationId
/// in stdout.
/// </param>
public record QueuedOrchestrationRequest(OrchestrationImage OrchestrationImage, string? CorrelationId);

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
    ValueTask<QueuedOrchestrationRequest> DequeueRequest(CancellationToken cancellationToken);
}

/// <summary>
/// Implementation of <see cref="IOrchestrationQueue"/> using a bounded channel for read/writing
/// </summary>
public class BoundedChannelOrchestrationQueue : IOrchestrationQueue
{
    private readonly IAppCache appCache;
    private readonly Channel<QueuedOrchestrationRequest> queue;

    public BoundedChannelOrchestrationQueue(int capacity, IAppCache appCache)
    {
        this.appCache = appCache;
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        };
        queue = Channel.CreateBounded<QueuedOrchestrationRequest>(options);
    }

    public ValueTask QueueRequest(OrchestrationImage orchestrationImage, CancellationToken cancellationToken)
    {
        // Save any processing by checking if 'orchestrated' cache key exists
        if (appCache.TryGetValue(CacheKeys.GetOrchestrationCacheKey(orchestrationImage.AssetId), out bool cached) &&
            cached)
        {
            return ValueTask.CompletedTask;
        }

        // Capture the correlation-id now - it won't be available on the thread that dequeues this
        var request = new QueuedOrchestrationRequest(orchestrationImage, CorrelationIdContext.Current);
        return queue.Writer.WriteAsync(request, cancellationToken);
    }

    public ValueTask<QueuedOrchestrationRequest> DequeueRequest(CancellationToken cancellationToken)
        => queue.Reader.ReadAsync(cancellationToken);
}
