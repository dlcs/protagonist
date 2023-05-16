using System.Text.Json.Nodes;
using Amazon.SQS;
using Amazon.SQS.Model;
using DLCS.AWS.Settings;
using DLCS.AWS.SQS.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.SQS;

/// <summary>
/// Base marker interface for queue listener
/// </summary>
internal interface IQueueListener
{
    /// <summary>
    /// Get value checking if listener is currently listening to a queue.
    /// Value will be null if queue has not been started
    /// </summary>
    bool? IsListening { get; }
    
    /// <summary>
    /// Name of queue being listened to
    /// </summary>
    string QueueName { get; }

    /// <summary>
    /// Start listening to specified queue.
    /// On successful handle message is deleted from queue.
    /// </summary>
    /// <param name="cancellationToken">Current cancellation token</param>
    void Listen(CancellationToken cancellationToken);
}

public class SqsListener<TMessageType> : IQueueListener
    where TMessageType : Enum
{
    private readonly IAmazonSQS client;
    private readonly SubscribedToQueue<TMessageType> subscribedToQueue;
    private readonly AWSSettings options;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<SqsListener<TMessageType>> logger;
    
    public SqsListener(
        IAmazonSQS client,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AWSSettings> options,
        SubscribedToQueue<TMessageType> subscribedToQueue,
        ILogger<SqsListener<TMessageType>> logger)
    {
        this.client = client;
        this.subscribedToQueue = subscribedToQueue;
        this.options = options.Value;
        this.logger = logger;
        this.serviceScopeFactory = serviceScopeFactory;
    }
    
    /// <summary>
    /// Get value checking if listener is currently listening to a queue.
    /// Value will be null if queue has not been started
    /// </summary>
    public bool? IsListening { get; private set; }

    /// <summary>
    /// Name of queue being listened to
    /// </summary>
    public string QueueName => subscribedToQueue.Name;
    
    public void Listen(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return;
        
        if (IsListening == true)
        {
            logger.LogWarning("Attempt to start listener for {QueueName} but already running", QueueName);
            return;
        }
            
        // kick off listener loop in the background - prevents calling code being blocked as code is infinite loop
        _ = Task.Run(async () =>
        {
            await ListenLoop(cancellationToken);
            IsListening = false;
            logger.LogWarning("Stopped listening to queue {QueueName}", QueueName);
        }, cancellationToken);

        IsListening = true;
    }

    /// <summary>
    /// Start listening to specified queue.
    /// On receipt a handler of type {T} is created DI container and used to handle request.
    /// On successful handle message is deleted from queue.
    /// </summary>
    /// <param name="cancellationToken">Current cancellation token</param>
    private async Task ListenLoop(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Listening to {QueueName}/{QueueUrl}", subscribedToQueue.Name, subscribedToQueue.Url);

        while (!cancellationToken.IsCancellationRequested)
        {
            ReceiveMessageResponse? response = null;
            var messageCount = 0;
            try
            {
                response = await GetMessagesFromQueue(subscribedToQueue.Url, cancellationToken);
                messageCount = response.Messages?.Count ?? 0;
            }
            catch (Exception ex)
            {
                // TODO - are there any specific issues to handle rather than generic? 
                logger.LogError(ex, "Error receiving messages on queue {Queue}", subscribedToQueue.Url);
            }

            if (messageCount == 0) continue;

            try
            {
                foreach (var message in response!.Messages!)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    var processed = await HandleMessage(message, cancellationToken);

                    if (processed)
                    {
                        await DeleteMessage(subscribedToQueue.Url, message, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in listen loop for queue {Queue}", subscribedToQueue.Url);
            }
        }
    }

    private Task<ReceiveMessageResponse> GetMessagesFromQueue(string queueUrl, CancellationToken cancellationToken)
        => client.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            WaitTimeSeconds = options.SQS.WaitTimeSecs,
            MaxNumberOfMessages = options.SQS.MaxNumberOfMessages,
        }, cancellationToken);

    private async Task<bool> HandleMessage(Message message, CancellationToken cancellationToken)
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("Handling message {Message} from {Queue}", message.MessageId, QueueName);
            }

            var queueMessage = new QueueMessage
            {
                Attributes = message.Attributes,
                Body = JsonNode.Parse(message.Body)!.AsObject(),
                MessageId = message.MessageId,
                QueueName = QueueName
            };

            // create a new scope to avoid issues with Scoped dependencies
            using var listenerScope = serviceScopeFactory.CreateScope();
            var handlerResolver =
                listenerScope.ServiceProvider.GetRequiredService<QueueHandlerResolver<TMessageType>>();
            var handler = handlerResolver(subscribedToQueue.MessageType);
            var processed = await handler.HandleMessage(queueMessage, cancellationToken);
            return processed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling message {MessageId} from queue {Queue}", message.MessageId,
                QueueName);
            return false;
        }
    }

    private Task DeleteMessage(string queueUrl, Message message, CancellationToken cancellationToken)
        => client.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = queueUrl,
            ReceiptHandle = message.ReceiptHandle
        }, cancellationToken);
}