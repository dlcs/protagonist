using DLCS.AWS.SQS;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using Microsoft.Extensions.Logging;

namespace CleanupHandler.Infrastructure;

public static class MessageParser
{
    public static DeletedNotificationRequest<T>? TryParseDeleteMessage<T>(QueueMessage message, ILogger logger) where T : IDeliverable
    {
        try
        {
            var request = message.GetMessageContents<DeletedNotificationRequest<T>>();

            if (request?.Deliverable?.GetAssetId() == null)
            {
                logger.LogInformation("Deserialised message but no id found");
                return null;
            }
            return request;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize notification {@Message}", message);
            return null;
        }
    }
    
    public static UpdatedNotificationRequest<T>? TryParseUpdatedMessage<T>(QueueMessage message, ILogger logger) where T : IDeliverable
    {
        try
        {
            var request = message.GetMessageContents<UpdatedNotificationRequest<T>>();

            if (request?.DeliverableBeforeUpdate?.GetAssetId() == null)
            {
                logger.LogInformation("Deserialised message but no 'before' id found");
                return null;
            }
            return request;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize notification {@Message}", message);
            return null;
        }
    }
}
