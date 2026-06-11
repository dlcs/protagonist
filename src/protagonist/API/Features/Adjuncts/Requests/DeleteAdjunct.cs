using API.Infrastructure;
using API.Infrastructure.Messaging.General;
using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.Adjuncts.Requests;

public class DeleteAdjunct(string id, AssetId assetId, ImageCacheType deleteFrom) : IRequest<ResultMessage<DeleteResult>>
{
    public string Id { get; } = id;
    
    public AssetId AssetId { get; } = assetId;

    public ImageCacheType DeleteFrom { get; } = deleteFrom;
}


public class DeleteAdjunctHandler(DlcsContext dbContext, IStorageRepository storageRepository, IDeliverableNotificationSender deliverableNotificationSender, ILogger<DeleteAdjunctHandler> logger)
    : IRequestHandler<DeleteAdjunct, ResultMessage<DeleteResult>>
{
    public async Task<ResultMessage<DeleteResult>> Handle(DeleteAdjunct request, CancellationToken cancellationToken)
    {
        var adjunct = await dbContext
            .Adjuncts
            .SingleOrDefaultAsync(a => a.Id == request.Id && a.AssetId == request.AssetId, cancellationToken);

        if (adjunct == null)
        {
            return new ResultMessage<DeleteResult>($"Couldn't find an adjunct with the id {request.Id}",
                DeleteResult.NotFound);
        }

        dbContext.Adjuncts.Remove(adjunct);

        if (adjunct.IsHosted())
        {
            await storageRepository.DecrementAdjunctStorage(adjunct.AssetId, adjunct.Size ?? 0, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await RaiseNotification(request, adjunct, cancellationToken);

        return new ResultMessage<DeleteResult>(string.Empty, DeleteResult.Deleted);
    }
    
    private async Task RaiseNotification(DeleteAdjunct request, Adjunct deletedAdjunct,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = NotificationRecord<Adjunct>.Delete(deletedAdjunct, request.DeleteFrom);
            logger.LogDebug("Sending delete adjunct notification for {AssetId}/{AdjunctId}", request.AssetId,
                request.Id);
            await deliverableNotificationSender.SendDeliverableModifiedMessage(deleted, cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't return error because notification failed
            logger.LogWarning(ex, "Error raising delete adjunct notification for {AssetId}/{AdjunctId}",
                request.AssetId, request.Id);
        }
    }
}
