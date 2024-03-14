using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.Processing;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Messaging;

public class IngestNotificationSender : IIngestNotificationSender
{
    private readonly ILogger<IngestNotificationSender> logger;
    private readonly IEngineClient engineClient;
    private readonly ICustomerQueueRepository customerQueueRepository;

    public IngestNotificationSender(
        IEngineClient engineClient,
        ICustomerQueueRepository customerQueueRepository,
        ILogger<IngestNotificationSender> logger)
    {
        this.engineClient = engineClient;
        this.logger = logger;
        this.customerQueueRepository = customerQueueRepository;
    }
    
    public async Task<bool> SendIngestAssetRequest(Asset assetToIngest, CancellationToken cancellationToken = default)
    {
        // Increment queue - do it before sending to avoid potential for message to immediately being picked up
        await customerQueueRepository.IncrementSize(assetToIngest.Customer, QueueNames.Default,
            cancellationToken: cancellationToken);
        
        var success = await engineClient.AsynchronousIngest(assetToIngest, cancellationToken);
        
        if (!success)
        {
            logger.LogWarning("Decrementing customer {Customer} 'default' queue as enqueue failed",
                assetToIngest.Customer);
            await customerQueueRepository.DecrementSize(assetToIngest.Customer, QueueNames.Default,
                cancellationToken: cancellationToken);
        }
        
        return success;
    }

    public async Task<int> SendIngestAssetsRequest(IReadOnlyList<Asset> assets, bool isPriority,
        CancellationToken cancellationToken = default)
    {
        if (assets.IsNullOrEmpty()) return 0;
        
        // Preemptively increment the queue size - if there's a particularly large batch the engine could have picked
        // up a few prior to this returning
        var queue = isPriority ? QueueNames.Priority : QueueNames.Default;
        var customerId = assets[0].Customer;
        await customerQueueRepository.IncrementSize(customerId, queue, assets.Count, cancellationToken);
        
        var sentCount = await engineClient.AsynchronousIngestBatch(assets, isPriority, cancellationToken);

        if (sentCount != assets.Count)
        {
            var difference = assets.Count - sentCount;
            logger.LogWarning(
                "Decrementing customer {Customer} '{QueueName}' queue by {FailedCount} as some messages failed",
                customerId, queue, difference);
            await customerQueueRepository.DecrementSize(customerId, queue, difference, cancellationToken);
        }
        
        return sentCount;
    }

    public async Task<HttpStatusCode> SendImmediateIngestAssetRequest(Asset assetToIngest, 
        CancellationToken cancellationToken = default)
    {
        var statusCode = await engineClient.SynchronousIngest(assetToIngest, cancellationToken);
        return statusCode;
    }
}