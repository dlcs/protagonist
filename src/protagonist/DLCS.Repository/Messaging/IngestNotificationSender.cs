using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.Processing;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Messaging;

public class IngestNotificationSender(
    IEngineClient engineClient,
    ICustomerQueueRepository customerQueueRepository,
    ILogger<IngestNotificationSender> logger)
    : IIngestNotificationSender
{
    public Task<bool> SendIngestAdjunctRequest(Adjunct adjunctToIngest, CancellationToken cancellationToken = default)
        => SendIngestRequest(adjunctToIngest, QueueNames.Adjunct, cancellationToken);

    public Task<int> SendIngestAdjunctRequest(IReadOnlyList<Adjunct> adjuncts,
        CancellationToken cancellationToken = default)
        => IngestDeliverable(
            adjuncts,
            QueueNames.Adjunct,
            () => engineClient.AsynchronousIngestBatch(adjuncts, cancellationToken),
            cancellationToken
        );

    public Task<bool> SendIngestAssetRequest(Asset assetToIngest, CancellationToken cancellationToken = default)
        => SendIngestRequest(assetToIngest, QueueNames.Default, cancellationToken);

    public Task<int> SendIngestAssetsRequest(IReadOnlyList<Asset> assets, bool isPriority,
        CancellationToken cancellationToken = default)
        => IngestDeliverable(
            assets,
            isPriority ? QueueNames.Priority : QueueNames.Default,
            () => engineClient.AsynchronousIngestBatch(assets, isPriority, cancellationToken),
            cancellationToken);

    private async Task<int> IngestDeliverable<T>(IReadOnlyList<T> deliverables, string queueName,
        Func<Task<int>> engineCall, CancellationToken cancellationToken)
        where T : IDeliverable
    {
        if (deliverables.IsNullOrEmpty()) return 0;
        
        var customerId = deliverables[0].GetAssetId().Customer;

        await customerQueueRepository.IncrementSize(customerId, queueName, deliverables.Count, cancellationToken);

        var sentCount = await engineCall();

        if (sentCount != deliverables.Count)
        {
            var difference = deliverables.Count - sentCount;
            logger.LogWarning(
                "Decrementing customer {Customer} '{QueueName}' queue by {FailedCount} as some messages failed",
                customerId, queueName, difference);
            await customerQueueRepository.DecrementSize(customerId, queueName, difference, cancellationToken);
        }

        return sentCount;
    }

    private async Task<bool> SendIngestRequest(IDeliverable deliverable, string queueName, CancellationToken cancellationToken)
    {
        var customerId = deliverable.GetAssetId().Customer;
        // Increment queue - do it before sending to avoid potential for message to immediately being picked up
        await customerQueueRepository.IncrementSize(customerId, queueName, cancellationToken: cancellationToken);
        
        var success = await engineClient.AsynchronousIngest(deliverable, cancellationToken);
        
        if (!success)
        {
            logger.LogWarning("Decrementing customer {Customer} '{QueueName}' queue as enqueue failed",
                customerId, queueName);
            await customerQueueRepository.DecrementSize(customerId, queueName,
                cancellationToken: cancellationToken);
        }
        
        return success;
    }

    public async Task<HttpStatusCode> SendImmediateIngestAssetRequest(Asset assetToIngest, 
        CancellationToken cancellationToken = default)
    {
        var statusCode = await engineClient.SynchronousIngest(assetToIngest, cancellationToken);
        return statusCode;
    }
}
