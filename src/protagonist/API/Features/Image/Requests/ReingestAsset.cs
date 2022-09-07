using System.Net;
using System.Threading;
using System.Threading.Tasks;
using API.Features.Assets;
using API.Infrastructure.Models;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Image.Requests;

/// <summary>
/// Reingest specified asset - this will 
/// </summary>
public class ReingestAsset : IRequest<AssetModifyResult<Asset>>
{
    public AssetId AssetId { get; }

    public ReingestAsset(int customer, int space, string asset)
    {
        AssetId = new AssetId(customer, space, asset);
    }
}

public class ReingestAssetHandler : IRequestHandler<ReingestAsset, AssetModifyResult<Asset>>
{
    private readonly IAssetNotificationSender assetNotificationSender;
    private readonly IApiAssetRepository assetRepository;
    private readonly ILogger<ReingestAssetHandler> logger;

    public ReingestAssetHandler(
        IAssetNotificationSender assetNotificationSender,
        IApiAssetRepository assetRepository,
        ILogger<ReingestAssetHandler> logger)
    {
        this.assetNotificationSender = assetNotificationSender;
        this.assetRepository = assetRepository;
        this.logger = logger;
    }
    
    public async Task<AssetModifyResult<Asset>> Handle(ReingestAsset request, CancellationToken cancellationToken)
    {
        var existingAsset = await assetRepository.GetAsset(request.AssetId, true);

        var validationException = ValidateAsset(existingAsset, request.AssetId);
        if (validationException != null) return validationException;
        
        var asset = await MarkAssetAsIngesting(cancellationToken, existingAsset!);
        
        await assetNotificationSender.SendAssetModifiedNotification(ChangeType.Update, existingAsset, asset);
        var statusCode = await assetNotificationSender.SendImmediateIngestAssetRequest(asset, false, cancellationToken);
        
        if (statusCode.IsSuccess())
        {
            logger.LogDebug("{AssetId} successfully reingested", request.AssetId);
            return AssetModifyResult<Asset>.Success(asset);
        }

        logger.LogDebug("{AssetId} error reingesting asset", request.AssetId);
        return statusCode switch
        {
            HttpStatusCode.BadRequest => AssetModifyResult<Asset>.Failure("Engine unable to process - bad request",
                UpdateResult.FailedValidation),
            HttpStatusCode.InsufficientStorage => AssetModifyResult<Asset>.Failure(
                "Engine unable to process - storage limit exceeded", UpdateResult.StorageLimitExceeded),
            _ => AssetModifyResult<Asset>.Failure("Unknown engine error", UpdateResult.Error)
        };
    }
    
    private AssetModifyResult<Asset>? ValidateAsset(Asset? asset, AssetId assetId)
    {
        if (asset == null)
        {
            return AssetModifyResult<Asset>.Failure($"Asset {assetId} not found", UpdateResult.NotFound);
        }

        if (asset.Family != AssetFamily.Image)
        {
            return AssetModifyResult<Asset>.Failure("Invalid operation - cannot reingest non-Image asset",
                UpdateResult.FailedValidation);
        }

        return null;
    }
    
    private async Task<Asset> MarkAssetAsIngesting(CancellationToken cancellationToken, Asset asset)
    {
        asset.SetFieldsForIngestion();
        var assetAfterSave = await assetRepository.Save(asset, cancellationToken);
        return assetAfterSave;
    }
}