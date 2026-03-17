using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using API.Infrastructure.Messaging.General;
using DLCS.Core.Collections;

namespace API.Infrastructure.Messaging.Adjunct;

public class AdjunctNotificationSender(
    ModificationSender notificationSender) : IAdjunctNotificationSender
{
    public Task SendAdjunctModifiedMessage(NotificationRecord<DLCS.Model.Assets.Adjunct> notification, CancellationToken cancellationToken = default)
        => SendAdjunctModifiedMessage(notification.AsList(), cancellationToken);

    public async Task SendAdjunctModifiedMessage(IReadOnlyCollection<NotificationRecord<DLCS.Model.Assets.Adjunct>> notifications, CancellationToken cancellationToken = default) =>
            await notificationSender.SendAdjunctModifiedMessage(notifications, adjunctSerialiserSettings,
                cancellationToken);
    
    private readonly JsonSerializerOptions adjunctSerialiserSettings = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { AdjunctSerialiserContractModifier }
        }
    };
    
    private static void AdjunctSerialiserContractModifier(JsonTypeInfo typeInfo)
    {
        // Collection of properties to ignore when serialising Adjunct object, by containing type
        var exclusionsByType = new Dictionary<Type, HashSet<string>>
        {
            [typeof(DLCS.Model.Assets.Adjunct)] = new(StringComparer.OrdinalIgnoreCase)
            {
                nameof(DLCS.Model.Assets.Adjunct.Asset)
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
