using DLCS.AWS.Settings;
using DLCS.AWS.SQS;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting SqsListenerService");
        var sqsSettings = awsSettings.Value.SQS;

        // Listeners will only start if setting has a value
        var startTasks = new List<Task>
        {
            sqsListenerManager.AddQueueListener(sqsSettings.ImageQueueName, EngineMessageType.Ingest),
            sqsListenerManager.AddQueueListener(sqsSettings.PriorityImageQueueName, EngineMessageType.Ingest),
            sqsListenerManager.AddQueueListener(sqsSettings.TimebasedQueueName, EngineMessageType.Ingest),
            sqsListenerManager.AddQueueListener(sqsSettings.TranscodeCompleteQueueName,
                EngineMessageType.TranscodeComplete)
        };
        await Task.WhenAll(startTasks);
        
        sqsListenerManager.StartListening();

        var configuredQueues = sqsListenerManager.GetConfiguredQueues();
        logger.LogInformation("Configured {QueueCount} queues", configuredQueues.Count);
        
        hostApplicationLifetime.ApplicationStopping.Register(OnStopping);
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