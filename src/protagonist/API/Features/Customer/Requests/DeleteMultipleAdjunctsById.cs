using System.Collections.Generic;
using API.Exceptions;
using API.Infrastructure;
using API.Infrastructure.Messaging.General;
using DLCS.Core.Exceptions;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Adjuncts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Customer.Requests;

public class DeleteMultipleAdjunctsById(
    IDictionary<string, List<string>> adjuncts,
    int customerId,
    ImageCacheType deleteFrom)
    : IRequest<int>
{
    public IDictionary<string, List<string>> Adjuncts { get; } = adjuncts;

    public int CustomerId { get; } = customerId;

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
        var adjunctsFromDatabase = GetRequestedAdjunctsFromDatabase(request);
        if (adjunctsFromDatabase.Count == 0) return 0;

        var rowCount = await DeleteAdjunctsFromDb(adjunctsFromDatabase, cancellationToken);
        logger.LogInformation("Deleted {DeletedRows} adjuncts from a requested {RequestedRows}", rowCount,
            request.Adjuncts.Count);
        
        await RaiseModifiedNotifications(adjunctsFromDatabase, request.DeleteFrom, cancellationToken);
        return adjunctsFromDatabase.Count;
    }
    
    private List<Adjunct> GetRequestedAdjunctsFromDatabase(DeleteMultipleAdjunctsById request)
    {
        var adjuncts = ConvertAssetIds(request.Adjuncts, request.CustomerId);
        
        return dlcsContext.Adjuncts.FindAdjuncts(adjuncts).ToList();
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

    private Dictionary<AssetId, List<string>> ConvertAssetIds(IDictionary<string, List<string>> adjuncts, int customerId)
    {
        try
        {
            var adjunctIds = adjuncts.ToDictionary(kvp => AssetId.FromString(kvp.Key), kvp => kvp.Value);
            
            if (adjunctIds.Keys.Any(a => a.Customer != customerId))
            {
                throw new BadRequestException("Asset id cannot belong to a different customer");
            }

            return adjunctIds;
        }
        catch (InvalidAssetIdException assetIdEx)
        {
            throw new BadRequestException(assetIdEx.Message, assetIdEx);
        }
    }
}

