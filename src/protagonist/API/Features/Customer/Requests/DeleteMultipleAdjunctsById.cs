using System.Collections.Generic;
using API.Infrastructure;
using API.Infrastructure.Messaging.General;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Adjuncts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Customer.Requests;

/// <summary>
/// Allows for the deletion of a list of adjuncts based on the id
/// </summary>
public class DeleteMultipleAdjunctsById(
    IDictionary<AssetId, List<string>> adjuncts,
    ImageCacheType deleteFrom)
    : IRequest<int>
{
    public IDictionary<AssetId, List<string>> Adjuncts { get; } = adjuncts;

    public ImageCacheType DeleteFrom { get; } = deleteFrom;
}

public class DeleteMultipleAdjunctsByIdHandler(
    DlcsContext dlcsContext,
    IDeliverableNotificationSender deliverableNotificationSender,
    ILogger<DeleteMultipleImagesByIdHandler> logger)
    : IRequestHandler<DeleteMultipleAdjunctsById, int>
{
    public async Task<int> Handle(DeleteMultipleAdjunctsById request, CancellationToken cancellationToken)
    {
        var adjunctsFromDatabase = dlcsContext.Adjuncts.FindAdjuncts(request.Adjuncts).ToList();
        if (adjunctsFromDatabase.Count == 0) return 0;

        var rowCount = await DeleteAdjunctsFromDb(adjunctsFromDatabase, cancellationToken);
        logger.LogInformation("Deleted {DeletedRows} adjuncts from a requested {RequestedRows}", rowCount,
            request.Adjuncts.Count);
        
        await RaiseModifiedNotifications(adjunctsFromDatabase, request.DeleteFrom, cancellationToken);
        return adjunctsFromDatabase.Count;
    }

    private async Task<int> DeleteAdjunctsFromDb(List<Adjunct> adjunctsFromDatabase, CancellationToken cancellationToken)
    {
        try
        {
            dlcsContext.Adjuncts.RemoveRange(adjunctsFromDatabase);
            var rowCount = await dlcsContext.SaveChangesAsync(cancellationToken);
            return rowCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting adjuncts from database");
            return 0;
        }
    }

    private async Task RaiseModifiedNotifications(List<Adjunct> adjuncts, ImageCacheType deleteFrom, CancellationToken cancellationToken)
    {
        var changeSet = adjuncts.Select(a => NotificationRecord<Adjunct>.Delete(a, deleteFrom)).ToList();
        await deliverableNotificationSender.SendDeliverableModifiedMessage(changeSet, cancellationToken);
    }
}

