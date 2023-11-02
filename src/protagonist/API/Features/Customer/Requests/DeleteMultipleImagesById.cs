using System.Collections.Generic;
using API.Features.Customer.Validation;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.PathElements;
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
    private readonly IIngestNotificationSender ingestNotificationSender;
    private readonly ILogger<DeleteMultipleImagesByIdHandler> logger;
    private readonly IPathCustomerRepository customerPathRepository;

    public DeleteMultipleImagesByIdHandler(
        DlcsContext dlcsContext,
        IIngestNotificationSender ingestNotificationSender,
        IPathCustomerRepository customerPathRepository,
        ILogger<DeleteMultipleImagesByIdHandler> logger)
    {
        this.dlcsContext = dlcsContext;
        this.ingestNotificationSender = ingestNotificationSender;
        this.customerPathRepository = customerPathRepository;
        this.logger = logger;
    }

    public async Task<int> Handle(DeleteMultipleImagesById request, CancellationToken cancellationToken)
    {
        var assetIds = ImageIdListValidation.ValidateRequest(request.AssetIds, request.CustomerId);
        var assetsFromDatabase = dlcsContext.Images
            .Where(i => i.Customer == request.CustomerId && assetIds.Contains(i.Id)).ToList();
        dlcsContext.Images.RemoveRange(assetsFromDatabase);

        logger.LogInformation("Deleted {DeletedRows} assets from a requested {RequestedRows}", assetsFromDatabase.Count,
            request.AssetIds.Count);

        if (assetsFromDatabase.Count == 0) return 0;
        
        var customerPathElement = await customerPathRepository.GetCustomerPathElement(request.CustomerId.ToString());
        await RaiseModifiedNotifications(assetsFromDatabase, customerPathElement);

        return assetsFromDatabase.Count;
    }

    private async Task RaiseModifiedNotifications(List<Asset> assets, CustomerPathElement customerPathElement)
    {
        foreach (var asset in assets)
        {
            await ingestNotificationSender.SendAssetModifiedNotification(ChangeType.Delete, asset, null, customerPathElement);
        }
    }
}