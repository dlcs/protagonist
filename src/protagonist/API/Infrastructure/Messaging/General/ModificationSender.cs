using System.Collections.Generic;
using System.Text.Json;
using DLCS.AWS.SNS;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.PathElements;
using Microsoft.Extensions.Logging;

namespace API.Infrastructure.Messaging.General;

public class ModificationSender(
    ITopicPublisher topicPublisher, 
    IPathCustomerRepository customerPathRepository,
    ILogger<ModificationSender> logger)
{
    public async Task SendAdjunctModifiedMessage<T>(IReadOnlyCollection<NotificationRecord<T>> notifications, 
        JsonSerializerOptions serializerOptions, CancellationToken cancellationToken = default) where T : class, IDeliverable
    {
        if (notifications.IsNullOrEmpty()) return;
        
        var changes = new List<DeliverableModifiedNotification>();
        
        foreach (var notification in notifications)
        {
            var serialisedNotification = await GetSerialisedNotification(notification, serializerOptions);
            if (serialisedNotification.HasText())
            {
                var attributes = new Dictionary<string, string>()
                {
                    { "messageType", notification.ChangeType.ToString() }
                };

                changes.Add(new DeliverableModifiedNotification(serialisedNotification, attributes));
            }
        }

        var typeParameterType = typeof(T);
        
        switch (typeParameterType) {
            case not null when typeParameterType == typeof(DLCS.Model.Assets.Adjunct):
                await topicPublisher.PublishToAdjunctModifiedTopic(changes, cancellationToken);
                break;
            case not null when typeParameterType == typeof(DLCS.Model.Assets.Asset):
                await topicPublisher.PublishToAssetModifiedTopic(changes, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Deliverable type not supported - {typeParameterType}");
        }
    }

    private async Task<string> GetSerialisedNotification<T>(NotificationRecord<T> notification, JsonSerializerOptions serializerOptions) where T : class, IDeliverable
    {
        if (notification.ChangeType == ChangeType.Create)
        {
            logger.LogDebug("Message Bus: {Type} Created: {AdjunctId}", typeof(T), notification.After!.Identifier());
            return await GetSerialisedCreatedNotification(notification.After!, serializerOptions);
        }
        
        if (notification.ChangeType == ChangeType.Delete)
        {
            logger.LogDebug("Message Bus: {Type} Deleted: {AdjunctId}", typeof(T), notification.Before!.Identifier());
            return await GetSerialisedDeletedNotification(notification.Before!, notification.DeleteFrom ?? ImageCacheType.None, serializerOptions);
        }
        
        logger.LogDebug("Message Bus: {Type} Modified: {AdjunctId}", typeof(T), notification.Before!.Identifier());
        return await GetSerialisedUpdatedNotification(notification.Before!, notification.After!,  serializerOptions);
    }

    private async Task<string> GetSerialisedDeletedNotification<T>(T deliverableBefore, ImageCacheType notificationDeleteFrom, JsonSerializerOptions serializerOptions)  where T : IDeliverable
    {
        var customerPathElement = await PathHelper.GetCustomerPathElement(deliverableBefore.GetAssetId().Customer, customerPathRepository);
        
        var request = new DeliverableDeletedNotification<T>
        {
            Deliverable = deliverableBefore,
            CustomerPathElement = customerPathElement,
            DeleteFrom = notificationDeleteFrom
        };

        return JsonSerializer.Serialize(request, serializerOptions);
    }

    private async Task<string> GetSerialisedUpdatedNotification<T>(T deliverableBefore, T deliverableAfter, JsonSerializerOptions serializerOptions) where T : IDeliverable
    {
        var customerPathElement = await PathHelper.GetCustomerPathElement(deliverableBefore.GetAssetId().Customer, customerPathRepository);
        
        var request = new DeliverableUpdatedNotification<T>
        {
            DeliverableBeforeUpdate = deliverableBefore,
            DeliverableAfterUpdate = deliverableAfter, 
            CustomerPathElement = customerPathElement
        };

        return JsonSerializer.Serialize(request, serializerOptions);
    }

    private async Task<string>GetSerialisedCreatedNotification<T>(T deliverableAfter, JsonSerializerOptions serializerOptions) where T : IDeliverable
    {
        
        var customerPathElement = await PathHelper.GetCustomerPathElement(deliverableAfter.GetAssetId().Customer, customerPathRepository);
        
        var request = new DeliverableCreatedNotification<T>
        {
            Deliverable = deliverableAfter,
            CustomerPathElement = customerPathElement
        };

        return JsonSerializer.Serialize(request, serializerOptions);
    }
}
