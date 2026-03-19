using CleanupHandler.Infrastructure;
using DLCS.AWS.SQS;
using DLCS.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanupHandler.Adjunct;

public class AdjunctDeletedHandler(
    IAdjunctBucketOperations adjunctBucketOperations,
    DlcsContext dbContext,
    IOptions<CleanupHandlerSettings> handlerSettings,
    ILogger<AdjunctDeletedHandler> logger)
    : IMessageHandler
{
    private readonly CleanupHandlerSettings settings = handlerSettings.Value;

    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken = default)
    {
        var request = MessageParser.TryParseDeleteMessage<DLCS.Model.Assets.Adjunct>(message, logger);
        if (request == null) return false;

        // This means it's not a hosted asset, so nothing to do
        if (request.Deliverable!.Origin == null)
        {
            return true;
        }

        var adjunct = request.Deliverable;
        
        // if the item exists in the db, assume the adjunct has been reingested after delete
        if (dbContext.Adjuncts.Any(a => a.Id == adjunct.Id && a.AssetId == adjunct.AssetId))
        {
            logger.LogInformation("Adjunct {Adjunct} can be found in the database, so will not be deleted",
                adjunct.Id);
            return true;
        }
        
        await adjunctBucketOperations.DeleteFromOriginBucket(adjunct, settings);
        
        return true;
    }
}
