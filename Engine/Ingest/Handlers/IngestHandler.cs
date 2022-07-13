using DLCS.AWS.SQS;

namespace Engine.Ingest.Handlers;

/// <summary>
/// Handler for ingest messages that have been pulled from queue.
/// </summary>
public class IngestHandler : IMessageHandler
{
    private readonly ILogger<IngestHandler> logger;

    public IngestHandler(ILogger<IngestHandler> logger)
    {
        this.logger = logger;
    }
    
    public Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received message {Message}", message.Body);
        return Task.FromResult(false);
    }
}