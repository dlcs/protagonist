using System.Text.Json.Nodes;
using Amazon.SQS;
using Amazon.SQS.Model;
using DLCS.AWS.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.SQS;

public interface IQueueListener
{
    /// <summary>
    /// Get value checking if listener is currently listening to a queue.
    /// Value will be null if queue has not been started
    /// </summary>
    public bool? IsListening { get; }
    
    /// <summary>
    /// Name of queue being listened to
    /// </summary>
    public string QueueName { get; }

    /// <summary>
    /// Start listening to specified queue.
    /// On successful handle message is deleted from queue.
    /// </summary>
    /// <param name="cancellationToken">Current cancellation token</param>
    void Listen(CancellationToken cancellationToken);
}

/// <summary>
/// Subscribes to SQS, using long polling to receive messages
/// </summary>
public class SqsListener<TMessageHandler> : IQueueListener
    where TMessageHandler : IMessageHandler
{
    private readonly IAmazonSQS client;
    private readonly AWSSettings options;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly SqsQueueUtilities queueUtilities;
    private readonly ILogger<SqsListener<TMessageHandler>> logger;
    
    public SqsListener(
        IAmazonSQS client,
        IOptions<AWSSettings> options,
        IServiceScopeFactory serviceScopeFactory,
        SqsQueueUtilities queueUtilities,
        ILogger<SqsListener<TMessageHandler>> logger,
        string queueName)
    {
        this.client = client;
        this.options = options.Value;
        this.serviceScopeFactory = serviceScopeFactory;
        this.queueUtilities = queueUtilities;
        this.logger = logger;
        QueueName = queueName;
    }
    
    /// <summary>
    /// Get value checking if listener is currently listening to a queue.
    /// Value will be null if queue has not been started
    /// </summary>
    public bool? IsListening { get; private set; }
    
    /// <summary>
    /// Name of queue being listened to
    /// </summary>
    public string QueueName { get; private set; }
    
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
        var queueUrl = await queueUtilities.GetQueueUrl(QueueName, cancellationToken);
        logger.LogInformation("Listening to {QueueName}/{QueueUrl}", QueueName, queueUrl);

        while (!cancellationToken.IsCancellationRequested)
        {
            ReceiveMessageResponse? response = null;
            var messageCount = 0;
            try
            {
                response = await GetMessagesFromQueue(queueUrl, cancellationToken);
                messageCount = response.Messages?.Count ?? 0;
            }
            catch (Exception ex)
            {
                // TODO - are there any specific issues to handle rather than generic? 
                logger.LogError(ex, "Error receiving messages on queue {Queue}", queueUrl);
            }

            if (messageCount == 0) continue;

            try
            {
                foreach (var message in response!.Messages!)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    var processed = await HandleMessage<TMessageHandler>(message, cancellationToken);

                    if (processed)
                    {
                        await DeleteMessage(queueUrl, message, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in listen loop for queue {Queue}", queueUrl);
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

    private async Task<bool> HandleMessage<T>(Message message, CancellationToken cancellationToken)
        where T : IMessageHandler
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("Handling message {Message} from {Queue}", message.MessageId, QueueName);
            }

            var messageBody = JsonNode.Parse(message.Body)!.AsObject();
            var queueMessage = new QueueMessage
            {
                Attributes = message.Attributes,
                Body = messageBody["Message"]!.ToString(),
                MessageId = message.MessageId,
            };

            // create a new scope to avoid issues with Scoped dependencies
            using var listenerScope = serviceScopeFactory.CreateScope();
            var handler = listenerScope.ServiceProvider.GetRequiredService<T>();

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