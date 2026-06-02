using CleanupHandler.Infrastructure;
using DLCS.AWS.SQS;
using DLCS.Repository;
using DLCS.Repository.Adjuncts;
using DLCS.Web.Logging;
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

        var adjunct = request.Deliverable!;
        using (LogContextHelpers.SetCorrelationId(message.MessageId))
        {
            // This means it's not a hosted asset, so nothing to do
            if (request.Deliverable!.Origin == null)
            {
                logger.LogDebug("Adjunct {Asset}/{Adjunct} does not have an origin, so no deletion required",
                    adjunct.AssetId, adjunct.Id);
                return true;
            }

            // if the item exists in the db, assume the adjunct has been reingested after delete
            if (dbContext.Adjuncts.FindAdjunct(adjunct.Id, adjunct.AssetId).Any())
            {
                logger.LogInformation("Adjunct {Asset}/{Adjunct} can be found in the database, so will not be deleted",
                    adjunct.AssetId, adjunct.Id);
                return true;
            }

            await adjunctBucketOperations.DeleteAdjunctStorage(adjunct, settings);
        }

        return true;
    }
}
