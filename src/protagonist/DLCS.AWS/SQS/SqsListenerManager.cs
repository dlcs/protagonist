using System.Collections.Concurrent;
using DLCS.AWS.SQS.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DLCS.AWS.SQS;

/// <summary>
/// Manages a collection of SQS listeners.
/// </summary>
public class SqsListenerManager
{
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly SqsQueueUtilities queueUtilities;
    private readonly ConcurrentBag<IQueueListener> listeners;
    private readonly object syncRoot = new();
    private readonly CancellationTokenSource cancellationTokenSource;

    public SqsListenerManager(IServiceScopeFactory serviceScopeFactory, SqsQueueUtilities queueUtilities)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.queueUtilities = queueUtilities;
        listeners = new ConcurrentBag<IQueueListener>();
        cancellationTokenSource = new CancellationTokenSource();
    }
    
    /// <summary>
    /// Configure listener for specified queue using given <see cref="IMessageHandler"/> handler type.
    /// This configures the queue only, it doesn't start listening. 
    /// </summary>
    /// <param name="queueName">Name of queue to listen to.</param>
    /// <param name="messageType">The type of message handled by this queue</param>
    /// <typeparam name="TMessageType">Enum that defines possible message types</typeparam>
    public async Task AddQueueListener<TMessageType>(string? queueName, TMessageType messageType)
        where TMessageType : Enum
    {
        if (string.IsNullOrWhiteSpace(queueName)) return;

        var queueUrl = await queueUtilities.GetQueueUrl(queueName);
        var subscribedToQueue = new SubscribedToQueue<TMessageType>(queueName, messageType, queueUrl);

        var serviceScope = serviceScopeFactory.CreateScope();
        var listener =
            ActivatorUtilities.CreateInstance<SqsListener<TMessageType>>(serviceScope.ServiceProvider,
                subscribedToQueue);
        listeners.Add(listener);
    }

    public async Task SetupDefaultQueue(string? queueName)
    {
        await AddQueueListener(queueName, SingleHandler.Default);
        StartListening();
    } 
    
    public void StartListening()
    {
        if (cancellationTokenSource.IsCancellationRequested) return;
        
        lock (syncRoot)
        {
            foreach (var listener in listeners)
            {
                if (listener.IsListening == true) continue;
                    
                listener.Listen(cancellationTokenSource.Token);
            }
        }
    }
    
    /// <summary>
    /// Signal all queue listeners to stop
    /// </summary>
    public void StopListening()
    {
        if (!cancellationTokenSource.IsCancellationRequested)
        {
            cancellationTokenSource.Cancel();
        }
    }

    /// <summary>
    /// Get a list of all queues currently being monitored
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<ConfiguredQueue> GetConfiguredQueues()
        => listeners.Select(l => new ConfiguredQueue(l.QueueName, l.IsListening)).ToList();
}

/// <summary>
/// Represents a queue that is configured for listening
/// </summary>
/// <param name="QueueName">Name of queue</param>
/// <param name="IsListening">Whether queue is currently listening</param>
public record ConfiguredQueue(string QueueName, bool? IsListening);