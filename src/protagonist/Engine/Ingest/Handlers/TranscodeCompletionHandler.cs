using DLCS.AWS.SQS;

namespace Engine.Ingest.Handlers;

/// <summary>
/// Handler for Transcode Completion messages.
/// </summary>
public class TranscodeCompletionHandler : IMessageHandler
{
    private readonly ILogger<TranscodeCompletionHandler> logger;

    public TranscodeCompletionHandler(ILogger<TranscodeCompletionHandler> logger)
    {
        this.logger = logger;
    }
    
    public Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received message {Message}", message.Body);
        return Task.FromResult(false);
    }
}