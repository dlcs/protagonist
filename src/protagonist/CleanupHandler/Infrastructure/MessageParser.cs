using DLCS.AWS.SQS;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using Microsoft.Extensions.Logging;

namespace CleanupHandler.Infrastructure;

public static class MessageParser
{
    /// <summary>
    /// Parses a delete message from an SQS message
    /// </summary>
    public static DeliverableDeletedNotification<T>? TryParseDeleteMessage<T>(QueueMessage message, ILogger logger) where T : IDeliverable
    {
        try
        {
            var request = message.GetMessageContents<DeliverableDeletedNotification<T>>();

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
    
    /// <summary>
    /// Parses an update message from an SQS message
    /// </summary>
    public static DeliverableUpdatedNotification<T>? TryParseUpdatedMessage<T>(QueueMessage message, ILogger logger) where T : IDeliverable
    {
        try
        {
            var request = message.GetMessageContents<DeliverableUpdatedNotification<T>>();

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
