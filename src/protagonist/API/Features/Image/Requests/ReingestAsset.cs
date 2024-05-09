using System.Net;
using API.Features.Assets;
using API.Infrastructure.Messaging;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Image.Requests;

/// <summary>
/// Reingest specified asset - this will trigger immediate/synchronous ingest of asset
/// </summary>
public class ReingestAsset : IRequest<ModifyEntityResult<Asset>>
{
    public AssetId AssetId { get; }

    public ReingestAsset(int customer, int space, string asset)
    {
        AssetId = new AssetId(customer, space, asset);
    }
}

public class ReingestAssetHandler : IRequestHandler<ReingestAsset, ModifyEntityResult<Asset>>
{
    private readonly IIngestNotificationSender ingestNotificationSender;
    private readonly IAssetNotificationSender assetNotificationSender;
    private readonly IApiAssetRepository assetRepository;
    private readonly ILogger<ReingestAssetHandler> logger;

    public ReingestAssetHandler(
        IIngestNotificationSender ingestNotificationSender,
        IAssetNotificationSender assetNotificationSender,
        IApiAssetRepository assetRepository,
        ILogger<ReingestAssetHandler> logger)
    {
        this.ingestNotificationSender = ingestNotificationSender;
        this.assetNotificationSender = assetNotificationSender;
        this.assetRepository = assetRepository;
        this.logger = logger;
    }
    
    public async Task<ModifyEntityResult<Asset>> Handle(ReingestAsset request, CancellationToken cancellationToken)
    {
        var existingAsset = await assetRepository.GetAsset(request.AssetId, true);

        var validationException = ValidateAsset(existingAsset, request.AssetId);
        if (validationException != null) return validationException;
        
        var asset = await MarkAssetAsIngesting(cancellationToken, existingAsset!);

        await assetNotificationSender.SendAssetModifiedMessage(AssetModificationRecord.Update(existingAsset!, asset),
            cancellationToken);
        var statusCode = await ingestNotificationSender.SendImmediateIngestAssetRequest(asset, cancellationToken);
        
        if (statusCode.IsSuccess())
        {
            logger.LogDebug("{AssetId} successfully reingested", request.AssetId);
            return ModifyEntityResult<Asset>.Success(asset);
        }

        logger.LogDebug("{AssetId} error reingesting asset", request.AssetId);
        return statusCode switch
        {
            HttpStatusCode.BadRequest => ModifyEntityResult<Asset>.Failure("Engine unable to process - bad request",
                WriteResult.FailedValidation),
            HttpStatusCode.InsufficientStorage => ModifyEntityResult<Asset>.Failure(
                "Engine unable to process - storage limit exceeded", WriteResult.StorageLimitExceeded),
            _ => ModifyEntityResult<Asset>.Failure("Unknown engine error", WriteResult.Error)
        };
    }
    
    private ModifyEntityResult<Asset>? ValidateAsset(Asset? asset, AssetId assetId)
    {
        if (asset == null)
        {
            return ModifyEntityResult<Asset>.Failure($"Asset {assetId} not found", WriteResult.NotFound);
        }

        if (asset.Family != AssetFamily.Image)
        {
            return ModifyEntityResult<Asset>.Failure("Invalid operation - cannot reingest non-Image asset",
                WriteResult.FailedValidation);
        }

        return null;
    }
    
    private async Task<Asset> MarkAssetAsIngesting(CancellationToken cancellationToken, Asset asset)
    {
        asset.Batch = 0;
        asset.SetFieldsForIngestion();
        var assetAfterSave = await assetRepository.Save(asset, true, cancellationToken);
        return assetAfterSave;
    }
}