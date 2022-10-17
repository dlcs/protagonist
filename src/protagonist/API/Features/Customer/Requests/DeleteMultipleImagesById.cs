using System.Collections.Generic;
using API.Features.Customer.Validation;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.Customer.Requests;

/// <summary>
/// Get a list of all images whose id is in ImageIds list
/// </summary>
public class DeleteMultipleImagesById : IRequest<int>
{
    public IReadOnlyCollection<string> AssetIds { get; }
    public int CustomerId { get; }

    public DeleteMultipleImagesById(IReadOnlyCollection<string> assetIds, int customerId)
    {
        AssetIds = assetIds;
        CustomerId = customerId;
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
        var assetIds = ImageIdListValidation.ValidateRequest(request.AssetIds, request.CustomerId);

        var deletedRows = await dlcsContext.Images.AsNoTracking()
            .Where(i => i.Customer == request.CustomerId && assetIds.Contains(i.Id))
            .DeleteFromQueryAsync(cancellationToken);

        logger.LogInformation("Deleted {DeletedRows} assets from a requested {RequestedRows}", deletedRows,
            request.AssetIds.Count);

        if (deletedRows == 0) return 0;

        await RaiseModifiedNotifications(request);

        return deletedRows;
    }

    private async Task RaiseModifiedNotifications(DeleteMultipleImagesById request)
    {
        // NOTE(DG) there is the possibility to raise a notification for an object that doesn't exist here,
        // we just issue a DELETE request to DB without checking which items exist 
        foreach (var asset in request.AssetIds.Select(i => new Asset { Id = AssetId.FromString(i) }))
        {
            await assetNotificationSender.SendAssetModifiedNotification(ChangeType.Delete, asset, null);
        }
    }
}