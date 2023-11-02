using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.PathElements;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Image.Requests;

/// <summary>
/// Delete asset from database and raise notification for further derivatives to be removed
/// </summary>
public class DeleteAsset : IRequest<DeleteResult>
{
    public AssetId AssetId { get; }
    
    public DeleteAsset(int customer, int space, string imageId)
    {
        AssetId = new AssetId(customer, space, imageId);
    }
}

public class DeleteAssetHandler : IRequestHandler<DeleteAsset, DeleteResult>
{
    private readonly IIngestNotificationSender ingestNotificationSender;
    private readonly IAssetRepository assetRepository;
    private readonly ILogger<DeleteAssetHandler> logger;
    private readonly IPathCustomerRepository customerPathRepository;

    public DeleteAssetHandler(
        IIngestNotificationSender ingestNotificationSender,
        IAssetRepository assetRepository,
        IPathCustomerRepository customerPathRepository,
        ILogger<DeleteAssetHandler> logger)
    {
        this.ingestNotificationSender = ingestNotificationSender;
        this.assetRepository = assetRepository;
        this.customerPathRepository = customerPathRepository;
        this.logger = logger;
    }
    
    public async Task<DeleteResult> Handle(DeleteAsset request, CancellationToken cancellationToken)
    {
        logger.LogDebug("Deleting asset {AssetId}", request.AssetId);
        var deleteResult = await assetRepository.DeleteAsset(request.AssetId);

        if (deleteResult.Result != DeleteResult.Deleted)
        {
            return deleteResult.Result;
        }

        try
        {
            var customerPathElement = await customerPathRepository.GetCustomerPathElement(request.AssetId.Customer.ToString());
            logger.LogDebug("Sending delete asset notification for {AssetId}", request.AssetId);
            await ingestNotificationSender.SendAssetModifiedNotification(ChangeType.Delete,
                deleteResult.DeletedEntity, null, customerPathElement);
        }
        catch (Exception ex)
        {
            // Don't return error because notification failed
            logger.LogWarning(ex, "Error raising delete notification for {AssetId}", request.AssetId);
        }

        return DeleteResult.Deleted;
    }
}