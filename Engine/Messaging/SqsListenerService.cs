using DLCS.AWS.Settings;
using Engine.Ingest.Handlers;
using Microsoft.Extensions.Options;

namespace Engine.Messaging;

/// <summary>
/// <see cref="BackgroundService"/> that starts listeners for all queues configured in settings
/// </summary>
public class SqsListenerService : BackgroundService
{
    private readonly IHostApplicationLifetime hostApplicationLifetime;
    private readonly IOptions<AWSSettings> awsSettings;
    private readonly SqsListenerManager sqsListenerManager;
    private readonly ILogger<SqsListenerService> logger;

    public SqsListenerService(
        IHostApplicationLifetime hostApplicationLifetime,
        IOptions<AWSSettings> awsSettings,
        SqsListenerManager sqsListenerManager,
        ILogger<SqsListenerService> logger)
    {
        this.hostApplicationLifetime = hostApplicationLifetime;
        this.awsSettings = awsSettings;
        this.sqsListenerManager = sqsListenerManager;
        this.logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting SqsListenerService");
        var sqsSettings = awsSettings.Value.SQS;

        // Listeners will only start if setting has a value
        sqsListenerManager.AddQueueListener<IngestHandler>(sqsSettings.ImageQueueName);
        sqsListenerManager.AddQueueListener<IngestHandler>(sqsSettings.PriorityImageQueueName);
        sqsListenerManager.AddQueueListener<IngestHandler>(sqsSettings.TimebasedQueueName);
        sqsListenerManager.AddQueueListener<TranscodeCompletionHandler>(sqsSettings.TranscodeCompleteQueueName);
        sqsListenerManager.StartListening();

        var configuredQueues = sqsListenerManager.GetConfiguredQueues();
        logger.LogInformation("Configured {QueueCount} queues", configuredQueues.Count);
        
        hostApplicationLifetime.ApplicationStopping.Register(OnStopping);
        
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning("Stopping SqsListenerService");
        return Task.CompletedTask;
    }
    
    private void OnStopping()
    {
        sqsListenerManager.StopListening();
        logger.LogInformation("Stopping listening to queues");
    }
}