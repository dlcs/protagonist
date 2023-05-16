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

public class AssetNotificationSender : IAssetNotificationSender
{
    private readonly ILogger<AssetNotificationSender> logger;
    private readonly IEngineClient engineClient;
    private readonly ICustomerQueueRepository customerQueueRepository;

    public AssetNotificationSender(
        IEngineClient engineClient,
        ICustomerQueueRepository customerQueueRepository,
        ILogger<AssetNotificationSender> logger)
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
        
        var ingestAssetRequest = new IngestAssetRequest(assetToIngest, DateTime.UtcNow);
        var success = await engineClient.AsynchronousIngest(ingestAssetRequest, cancellationToken);
        
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
        
        var ingestAssetRequests = assets.Select(a => new IngestAssetRequest(a, DateTime.UtcNow)).ToList();
        var sentCount = await engineClient.AsynchronousIngestBatch(ingestAssetRequests, isPriority, cancellationToken);

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

    public async Task<HttpStatusCode> SendImmediateIngestAssetRequest(Asset assetToIngest, bool derivativesOnly,
        CancellationToken cancellationToken = default)
    {
        var ingestAssetRequest = new IngestAssetRequest(assetToIngest, DateTime.UtcNow);
        var statusCode = await engineClient.SynchronousIngest(ingestAssetRequest, derivativesOnly, cancellationToken);
        return statusCode;
    }

    public Task SendAssetModifiedNotification(ChangeType changeType, Asset? before, Asset? after)
    {
        /*
         * TODO - this should probably have a bulk implementation, assuming it handles bulk enqueuing of messages
         * it's more efficient to do in batches rather than 1 at a time (like engine client)
         */
        switch (changeType)
        {
            case ChangeType.Create when before != null:
                throw new ArgumentException("Asset Creation cannot have a before asset", nameof(before));
            case ChangeType.Create when after == null:
                throw new ArgumentException("Asset Creation must have an after asset", nameof(after));
            case ChangeType.Update when before == null:
                throw new ArgumentException("Asset Update must have a before asset", nameof(before));
            case ChangeType.Update when after == null:
                throw new ArgumentException("Asset Update must have an after asset", nameof(after));
            case ChangeType.Delete when before == null:
                throw new ArgumentException("Asset Delete must have a before asset", nameof(before));
            case ChangeType.Delete when after != null:
                throw new ArgumentException("Asset Delete cannot have an after asset", nameof(after));
            default:
                logger.LogDebug("Message Bus: Asset Modified: {AssetId}", after?.Id ?? before.Id);
                break;
        }
        
        return Task.CompletedTask;;
    }
}