using DLCS.AWS.Settings;
using DLCS.AWS.SQS;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanupHandler.Infrastructure;

/// <summary>
/// Background worker that monitors SQS queue for delete notifications
/// </summary>
public class DeleteQueueMonitor : BackgroundService
{
    private readonly IHostApplicationLifetime hostApplicationLifetime;
    private readonly IOptions<AWSSettings> awsSettings;
    private readonly SqsListenerManager sqsListenerManager;
    private readonly ILogger<DeleteQueueMonitor> logger;

    public DeleteQueueMonitor(SqsListenerManager sqsListenerManager, ILogger<DeleteQueueMonitor> logger,
        IHostApplicationLifetime hostApplicationLifetime, IOptions<AWSSettings> awsSettings)
    {
        this.sqsListenerManager = sqsListenerManager;
        this.logger = logger;
        this.hostApplicationLifetime = hostApplicationLifetime;
        this.awsSettings = awsSettings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting DeleteQueueMonitor");
        
        await sqsListenerManager.SetupDefaultQueue(awsSettings.Value.SQS.DeleteNotificationQueueName);
        hostApplicationLifetime.ApplicationStopping.Register(OnStopping);
    }
    
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning("Stopping DeleteQueueMonitor");
        return Task.CompletedTask;
    }
    
    private void OnStopping()
    {
        sqsListenerManager.StopListening();
        logger.LogInformation("Stopping listening to queues");
    }
}