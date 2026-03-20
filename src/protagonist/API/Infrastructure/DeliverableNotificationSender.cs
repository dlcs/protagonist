using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using API.Infrastructure.Messaging.General;
using DLCS.Core.Collections;
using DLCS.Model.Assets;

namespace API.Infrastructure;

/// <summary>
/// Class that handles raising notifications for modifications made to assets (Create/Update/Delete)
/// </summary>
public class DeliverableNotificationSender(
    ModificationSender notificationSender)
    : IDeliverableNotificationSender
{
    public Task SendDeliverableModifiedMessage<T>(NotificationRecord<T> notification,
        CancellationToken cancellationToken = default) where T : class, IDeliverable
        => SendDeliverableModifiedMessage(notification.AsList(), cancellationToken);

    public async Task SendDeliverableModifiedMessage<T>(IReadOnlyCollection<NotificationRecord<T>> notifications,
        CancellationToken cancellationToken = default) where T : class, IDeliverable =>
        await notificationSender.SendModifiedMessage(notifications, assetSerialiserSettings,
            cancellationToken);
    
    private readonly JsonSerializerOptions assetSerialiserSettings = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { AssetSerialiserContractModifier }
        }
    };

    private static void AssetSerialiserContractModifier(JsonTypeInfo typeInfo)
    {
        // Collection of properties to ignore when serialising Asset object, by containing type
        var exclusionsByType = new Dictionary<Type, HashSet<string>>
        {
            [typeof(Asset)] = new(StringComparer.OrdinalIgnoreCase)
            {
                nameof(Asset.BatchAssets),
                nameof(Asset.ImageOptimisationPolicy),
                nameof(Asset.ThumbnailPolicy),
                nameof(Asset.Adjuncts)
            },
            [typeof(ImageDeliveryChannel)] = new(StringComparer.OrdinalIgnoreCase)
            {
                nameof(ImageDeliveryChannel.DeliveryChannelPolicy)
            },
            [typeof(Adjunct)] = new(StringComparer.OrdinalIgnoreCase)
            {
                nameof(Adjunct.Asset)
            }
        };

        if (!exclusionsByType.TryGetValue(typeInfo.Type, out var exclusions)) return;

        foreach (var prop in typeInfo.Properties)
        {
            if (exclusions.Contains(prop.Name))
            {
                prop.ShouldSerialize = static (_, _) => false;
            }
        }
    }
}
