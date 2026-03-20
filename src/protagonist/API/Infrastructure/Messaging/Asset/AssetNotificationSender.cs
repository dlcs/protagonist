using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using API.Infrastructure.Messaging.General;
using DLCS.Core.Collections;
using DLCS.Model.Assets;

namespace API.Infrastructure.Messaging.Asset;

/// <summary>
/// Class that handles raising notifications for modifications made to assets (Create/Update/Delete)
/// </summary>
public class AssetNotificationSender(
    ModificationSender notificationSender)
    : IAssetNotificationSender
{
    public Task SendAssetModifiedMessage(NotificationRecord<DLCS.Model.Assets.Asset> notification,
        CancellationToken cancellationToken = default)
        => SendAssetModifiedMessage(notification.AsList(), cancellationToken);

    public async Task SendAssetModifiedMessage(IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Asset>> notifications,
        CancellationToken cancellationToken = default) =>
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
            [typeof(DLCS.Model.Assets.Asset)] = new(StringComparer.OrdinalIgnoreCase)
            {
                nameof(DLCS.Model.Assets.Asset.BatchAssets), 
                nameof(DLCS.Model.Assets.Asset.ImageOptimisationPolicy), 
                nameof(DLCS.Model.Assets.Asset.ThumbnailPolicy), 
                nameof(DLCS.Model.Assets.Asset.Adjuncts)
            },
            [typeof(ImageDeliveryChannel)] = new(StringComparer.OrdinalIgnoreCase)
            {
                nameof(ImageDeliveryChannel.DeliveryChannelPolicy)
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
