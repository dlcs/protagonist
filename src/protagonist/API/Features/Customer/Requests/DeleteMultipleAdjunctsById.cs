using System.Collections.Generic;
using API.Infrastructure;
using API.Infrastructure.Messaging.General;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Repository;
using DLCS.Repository.Adjuncts;
using DLCS.Repository.Storage;
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
    IStorageRepository storageRepository,
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

        if (rowCount == 0) return 0;

        await DecrementStorageForHostedAdjuncts(adjunctsFromDatabase, cancellationToken);
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

    private async Task DecrementStorageForHostedAdjuncts(List<Adjunct> adjuncts, CancellationToken cancellationToken)
    {
        var hostedAdjuncts = adjuncts.Where(a => a.IsHosted()).ToList();
        if (hostedAdjuncts.Count == 0) return;

        foreach (var adjunct in hostedAdjuncts)
        {
            var size = adjunct.Size ?? 0;
            await storageRepository.DecrementAdjunctStorage(
                adjunct.AssetId.Customer, adjunct.AssetId.Space, size, cancellationToken);
            await dlcsContext.ImageStorages.DecrementAdjunctSize(adjunct.AssetId, size, cancellationToken);
        }
    }

    private async Task RaiseModifiedNotifications(List<Adjunct> adjuncts, ImageCacheType deleteFrom, CancellationToken cancellationToken)
    {
        var changeSet = adjuncts.Select(a => NotificationRecord<Adjunct>.Delete(a, deleteFrom)).ToList();
        await deliverableNotificationSender.SendDeliverableModifiedMessage(changeSet, cancellationToken);
    }
}
