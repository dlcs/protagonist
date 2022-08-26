using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Features.Images.Orchestration;

/// <summary>
/// BackgroundService that monitors queue for requests to orchestrate images.
/// </summary>
public class OrchestrationQueueMonitor : BackgroundService
{
    private readonly IOrchestrationQueue orchestrationQueue;
    private readonly IImageOrchestrator imageOrchestrator;
    private readonly ILogger<OrchestrationQueueMonitor> logger;

    public OrchestrationQueueMonitor(IOrchestrationQueue orchestrationQueue, IImageOrchestrator imageOrchestrator,
        ILogger<OrchestrationQueueMonitor> logger)
    {
        this.orchestrationQueue = orchestrationQueue;
        this.imageOrchestrator = imageOrchestrator;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting OrchestrationQueueMonitor");

        await BackgroundProcessor(stoppingToken);
    }
    
    private async Task BackgroundProcessor(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var orchestrationImage = await orchestrationQueue.DequeueRequest(stoppingToken);

            try
            {
                logger.LogTrace("Processing queued orchestration request for {AssetId}", orchestrationImage.AssetId);
                await imageOrchestrator.EnsureImageOrchestrated(orchestrationImage, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred orchestrating image {AssetId}", orchestrationImage.AssetId);
            }
        }
    }
}