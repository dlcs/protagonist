using DLCS.AWS.Settings;
using DLCS.AWS.SQS;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanupHandler.Infrastructure;

/// <summary>
/// Background worker that monitors SQS queue for cleanup notifications
/// </summary>
public class CleanupHandlerQueueMonitor(
    SqsListenerManager sqsListenerManager,
    ILogger<CleanupHandlerQueueMonitor> logger,
    IHostApplicationLifetime hostApplicationLifetime,
    IOptions<AWSSettings> awsSettings)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting queues in cleanup handler");
        
        var startTasks = new List<Task>
        {
            sqsListenerManager.AddQueueListener(awsSettings.Value.SQS.DeleteNotificationQueueName, CleanupMessageQueueType.DeleteAsset),
            sqsListenerManager.AddQueueListener(awsSettings.Value.SQS.UpdateNotificationQueueName, CleanupMessageQueueType.UpdateAsset),
            sqsListenerManager.AddQueueListener(awsSettings.Value.SQS.AdjunctDeleteNotificationQueueName, CleanupMessageQueueType.DeleteAdjunct),
            sqsListenerManager.AddQueueListener(awsSettings.Value.SQS.AdjunctUpdateNotificationQueueName, CleanupMessageQueueType.UpdateAdjunct),
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
