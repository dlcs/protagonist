using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Web.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Features.Images.Orchestration;

/// <summary>
/// BackgroundService that monitors queue for requests to orchestrate images.
/// </summary>
public class OrchestrationQueueMonitor(
    IOrchestrationQueue orchestrationQueue,
    IImageOrchestrator imageOrchestrator,
    ILogger<OrchestrationQueueMonitor> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting OrchestrationQueueMonitor");

        await BackgroundProcessor(stoppingToken);
    }
    
    private async Task BackgroundProcessor(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var (orchestrationImage, correlationId) = await orchestrationQueue.DequeueRequest(stoppingToken);

            // Re-establish the correlation-id of the request that queued this, so orchestration can be tied back to it
            using var _ = LogContextHelpers.SetCorrelationId(correlationId ?? $"n_{Guid.NewGuid().ToString()}");

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
