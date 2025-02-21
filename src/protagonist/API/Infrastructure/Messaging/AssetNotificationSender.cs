using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using DLCS.AWS.SNS;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.PathElements;
using Microsoft.Extensions.Logging;

namespace API.Infrastructure.Messaging;

/// <summary>
/// Class that handles raising notifications for modifications made to assets (Create/Update/Delete)
/// </summary>
public class AssetNotificationSender : IAssetNotificationSender
{
    private readonly ILogger<AssetNotificationSender> logger;
    private readonly ITopicPublisher topicPublisher;
    private readonly IPathCustomerRepository customerPathRepository;
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    private readonly Dictionary<int, CustomerPathElement> customerPathElements = new();

    public AssetNotificationSender(
        ITopicPublisher topicPublisher,
        IPathCustomerRepository customerPathRepository,
        ILogger<AssetNotificationSender> logger)
    {
        this.logger = logger;
        this.topicPublisher = topicPublisher;
        this.customerPathRepository = customerPathRepository;
    }

    public Task SendAssetModifiedMessage(AssetModificationRecord notification,
        CancellationToken cancellationToken = default)
        => SendAssetModifiedMessage(notification.AsList(), cancellationToken);

    public async Task SendAssetModifiedMessage(IReadOnlyCollection<AssetModificationRecord> notifications,
        CancellationToken cancellationToken = default)
    {
        if (notifications.IsNullOrEmpty()) return;
        
        var changes = new List<AssetModifiedNotification>();
        
        foreach (var notification in notifications)
        {
            var serialisedNotification = await GetSerialisedNotification(notification);
            if (serialisedNotification.HasText())
            {
                var attributes = new Dictionary<string, string>()
                {
                    { "messageType", notification.ChangeType.ToString() }
                };
                
                if (notification.EngineNotified)
                {
                    attributes.Add("engineNotified", "True");
                }

                changes.Add(new AssetModifiedNotification(serialisedNotification, attributes));
            }
        }

        await topicPublisher.PublishToAssetModifiedTopic(changes, cancellationToken);
    }

    private async Task<string?> GetSerialisedNotification(AssetModificationRecord notification)
    {
        if (notification.ChangeType == ChangeType.Create)
        {
            logger.LogDebug("Message Bus: Asset Created: {AssetId}", notification.After!.Id);
            return await GetSerialisedAssetCreatedNotification(notification.After!);
        }
        
        if (notification.ChangeType == ChangeType.Delete)
        {
            logger.LogDebug("Message Bus: Asset Deleted: {AssetId}", notification.Before!.Id);
            return await GetSerialisedAssetDeletedNotification(notification.Before!, notification.DeleteFrom ?? ImageCacheType.None);
        }
        
        logger.LogDebug("Message Bus: Asset Modified: {AssetId}", notification.Before!.Id);
        return await GetSerialisedAssetUpdatedNotification(notification.Before!, notification.After!);
    }
    
    private async Task<string> GetSerialisedAssetDeletedNotification(Asset modifiedAsset, ImageCacheType deleteFrom)
    {
        var customerPathElement = await GetCustomerPathElement(modifiedAsset.Customer);
        
        var request = new AssetDeletedNotificationRequest
        {
            Asset = modifiedAsset,
            CustomerPathElement = customerPathElement,
            DeleteFrom = deleteFrom
        };

        return JsonSerializer.Serialize(request, settings);
    }
    
    private async Task<string> GetSerialisedAssetCreatedNotification(Asset modifiedAsset)
    {
        var customerPathElement = await GetCustomerPathElement(modifiedAsset.Customer);
        
        var request = new AssetCreatedNotificationRequest()
        {
            Asset = modifiedAsset,
            CustomerPathElement = customerPathElement
        };

        return JsonSerializer.Serialize(request, settings);
    }
    
    private async Task<string> GetSerialisedAssetUpdatedNotification(Asset modifiedAssetBefore, Asset modifiedAssetAfter)
    {
        var customerPathElement = await GetCustomerPathElement(modifiedAssetBefore.Customer);
        
        var request = new AssetUpdatedNotificationRequest()
        {
            AssetBeforeUpdate = modifiedAssetBefore,
            AssetAfterUpdate = modifiedAssetAfter, 
            CustomerPathElement = customerPathElement
        };

        return JsonSerializer.Serialize(request, settings);
    }
    
    private async Task<CustomerPathElement> GetCustomerPathElement(int customer)
    {
        if (customerPathElements.TryGetValue(customer, out var prefetchedCustomer)) return prefetchedCustomer;
        
        var customerPathElement = await customerPathRepository.GetCustomerPathElement(customer.ToString());
        customerPathElements[customer] = customerPathElement;
        return customerPathElement;
    }
}