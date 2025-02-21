using System.Collections.Generic;
using API.Features.Customer.Validation;
using API.Infrastructure.Messaging;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Customer.Requests;

/// <summary>
/// Get a list of all images whose id is in ImageIds list
/// </summary>
public class DeleteMultipleImagesById : IRequest<int>
{
    public IReadOnlyCollection<string> AssetIds { get; }
    public int CustomerId { get; }
    
    public ImageCacheType DeleteFrom { get; }

    public DeleteMultipleImagesById(IReadOnlyCollection<string> assetIds, int customerId, ImageCacheType deleteFrom)
    {
        AssetIds = assetIds;
        CustomerId = customerId;
        DeleteFrom = deleteFrom;
    }
}

public class DeleteMultipleImagesByIdHandler : IRequestHandler<DeleteMultipleImagesById, int>
{
    private readonly DlcsContext dlcsContext;
    private readonly IAssetNotificationSender assetNotificationSender;
    private readonly ILogger<DeleteMultipleImagesByIdHandler> logger;

    public DeleteMultipleImagesByIdHandler(
        DlcsContext dlcsContext,
        IAssetNotificationSender assetNotificationSender,
        ILogger<DeleteMultipleImagesByIdHandler> logger)
    {
        this.dlcsContext = dlcsContext;
        this.assetNotificationSender = assetNotificationSender;
        this.logger = logger;
    }

    public async Task<int> Handle(DeleteMultipleImagesById request, CancellationToken cancellationToken)
    {
        var assetsFromDatabase = GetRequestedAssetsFromDatabase(request);
        if (assetsFromDatabase.Count == 0) return 0;

        var rowCount = await DeleteAssetsFromDb(assetsFromDatabase, cancellationToken);
        logger.LogInformation("Deleted {DeletedRows} assets from a requested {RequestedRows}", rowCount,
            request.AssetIds.Count);
        
        await RaiseModifiedNotifications(assetsFromDatabase, request.DeleteFrom, cancellationToken);
        return assetsFromDatabase.Count;
    }
    
    private List<Asset> GetRequestedAssetsFromDatabase(DeleteMultipleImagesById request)
    {
        var assetIds = ImageIdListValidation.ValidateRequest(request.AssetIds, request.CustomerId);
        var assetsFromDatabase = dlcsContext.Images
            .Where(i => i.Customer == request.CustomerId && assetIds.Contains(i.Id)).ToList();
        return assetsFromDatabase;
    }

    private async Task<int> DeleteAssetsFromDb(List<Asset> assetsFromDatabase, CancellationToken cancellationToken)
    {
        try
        {
            dlcsContext.Images.RemoveRange(assetsFromDatabase);
            var rowCount = await dlcsContext.SaveChangesAsync(cancellationToken);
            return rowCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting assets from database");
            return 0;
        }
    }

    private async Task RaiseModifiedNotifications(List<Asset> assets, ImageCacheType deleteFrom, CancellationToken cancellationToken)
    {
        var changeSet = assets.Select(a => AssetModificationRecord.Delete(a, deleteFrom)).ToList();
        await assetNotificationSender.SendAssetModifiedMessage(changeSet, cancellationToken);
    }
}