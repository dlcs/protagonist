using System.Collections.Generic;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using MediatR;
using Microsoft.Extensions.Logging;
using OperationType = DLCS.Model.CustomerImage.OperationType;

namespace API.Features.Customer.Requests;

public class UpdateAllImages(List<AssetId> assetIds,  int customerId, List<string>? value, string field, 
    OperationType operation)
    : IRequest<ModifyEntityResult<List<Asset>>>
{
    public List<AssetId> AssetIds { get; } = assetIds;

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
            var assets = await bulkAssetPatcher.UpdateAssets(request, cancellationToken);

            return ModifyEntityResult<List<Asset>>.Success(assets);
        }
        catch (InvalidOperationException e)
        {
            logger.LogError(e, "Failed to update assets {Assets}", request.AssetIds);
            return ModifyEntityResult<List<Asset>>.Failure(e.Message, WriteResult.BadRequest);
        }
    }
}
