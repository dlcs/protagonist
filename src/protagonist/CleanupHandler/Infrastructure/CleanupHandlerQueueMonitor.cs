using DLCS.AWS.Settings;
using DLCS.AWS.SQS;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanupHandler.Infrastructure;

/// <summary>
/// Background worker that monitors SQS queue for cleanup notifications
/// </summary>
public class CleanupHandlerQueueMonitor : BackgroundService
{
    private readonly IHostApplicationLifetime hostApplicationLifetime;
    private readonly IOptions<AWSSettings> awsSettings;
    private readonly SqsListenerManager sqsListenerManager;
    private readonly ILogger<CleanupHandlerQueueMonitor> logger;

    public CleanupHandlerQueueMonitor(SqsListenerManager sqsListenerManager, ILogger<CleanupHandlerQueueMonitor> logger,
        IHostApplicationLifetime hostApplicationLifetime, IOptions<AWSSettings> awsSettings)
    {
        this.sqsListenerManager = sqsListenerManager;
        this.logger = logger;
        this.hostApplicationLifetime = hostApplicationLifetime;
        this.awsSettings = awsSettings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting queues in cleanup handler");
        
        var startTasks = new List<Task>
        {
            sqsListenerManager.AddQueueListener(awsSettings.Value.SQS.DeleteNotificationQueueName, AssetQueueType.Delete),
            sqsListenerManager.AddQueueListener(awsSettings.Value.SQS.UpdateNotificationQueueName, AssetQueueType.Update),
        };
        
        await Task.WhenAll(startTasks);
        
        sqsListenerManager.StartListening();

        var configuredQueues = sqsListenerManager.GetConfiguredQueues();
        logger.LogInformation("Configured {QueueCount} queues", configuredQueues.Count);
        
        hostApplicationLifetime.ApplicationStopping.Register(OnStopping);
    }
    
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning("Stopping CleanupHandlerQueueMonitor");
        return Task.CompletedTask;
    }
    
    private void OnStopping()
    {
        sqsListenerManager.StopListening();
        logger.LogInformation("Stopping listening to queues");
    }
}