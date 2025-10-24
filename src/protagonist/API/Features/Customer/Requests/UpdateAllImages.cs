using System.Collections.Generic;
using API.Features.Customer.Validation;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Customer.Requests;

public class UpdateAllImages(List<string> assetIds,  int customerId, List<string>? value, string field, 
    OperationType operation)
    : IRequest<ModifyEntityResult<List<Asset>>>
{
    public List<string> AssetIds { get; } = assetIds;

    public List<string>? Value { get; } = value;

    public string Field { get; } = field;
    
    public OperationType Operation { get; } = operation;

    public int CustomerId { get; } = customerId;
}

public class UpdateAllImagesHandler(IBulkAssetPatcher bulkAssetPatcher, ILogger<UpdateAllImagesHandler> logger)
    : IRequestHandler<UpdateAllImages, ModifyEntityResult<List<Asset>>>
{
    public async Task<ModifyEntityResult<List<Asset>>> Handle(UpdateAllImages request, CancellationToken cancellationToken)
    {
        try
        {
            var assetIds = ImageIdListValidation.ValidateRequest(request.AssetIds, request.CustomerId);
            var assets = await bulkAssetPatcher.UpdateAssets(assetIds, request.Value, request.Operation, request.Field,
                request.CustomerId, cancellationToken);

            return ModifyEntityResult<List<Asset>>.Success(assets);
        }
        catch (InvalidOperationException e)
        {
            logger.LogError(e, "Failed to update assets {Assets}", request.AssetIds);
            return ModifyEntityResult<List<Asset>>.Failure(e.Message, WriteResult.BadRequest);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unknown error updating assets {Assets}", request.AssetIds);
            return ModifyEntityResult<List<Asset>>.Failure(e.Message);
        }
    }
}
