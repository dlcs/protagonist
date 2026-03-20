using CleanupHandler.Asset;
using CleanupHandler.Infrastructure;
using DLCS.AWS.SQS;
using DLCS.Repository;
using DLCS.Web.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanupHandler.Adjunct;

public class AdjunctUpdatedHandler(
    IOptions<CleanupHandlerSettings> handlerSettings,
    IAdjunctBucketOperations adjunctBucketOperations,
    DlcsContext dlcsContext,
    ILogger<AssetUpdatedHandler> logger) : IMessageHandler
{
    private readonly CleanupHandlerSettings settings = handlerSettings.Value;
    
    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken = default)
    {
        var request = MessageParser.TryParseUpdatedMessage<DLCS.Model.Assets.Adjunct>(message, logger);
        if (request == null) return false;
        
        using (LogContextHelpers.SetCorrelationId(message.MessageId))
        {
            var adjunctBefore = request.DeliverableBeforeUpdate!;

            var adjunctAfter = await dlcsContext.Adjuncts.FirstOrDefaultAsync(a => a.Id == request.DeliverableAfterUpdate!.Id);

            if (adjunctAfter == null)
            {
                logger.LogInformation("Adjunct {Id} was not found in the database for use in after calculation",
                    adjunctBefore.Id);
                return false;
            }
            
            logger.LogDebug("Processing update adjunct notification for {Id}", adjunctBefore.Id);

            if (NoCleanupRequired(adjunctAfter, adjunctBefore))
            {
                logger.LogDebug("No cleanup required, aborting");
                return true;
            }
            
            await adjunctBucketOperations.DeleteFromOriginBucket(adjunctBefore, settings);

            return true;
        }
    }

    // We only cleanup when the adjunct has moved from hosted to unhosted
    private bool NoCleanupRequired(DLCS.Model.Assets.Adjunct adjunctAfter,
        DLCS.Model.Assets.Adjunct adjunctBefore) => adjunctBefore.Origin == null && adjunctAfter.Origin != null && adjunctAfter.ExternalId == null;
}
