using System.Collections.Generic;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.HydraModel;
using DLCS.Model;
using DLCS.Model.Assets;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Customer.Requests;

public class UpdateAllImages(BulkPatch<IdentifierOnly> hydraBulkPatch, int customerId)
    : IRequest<ModifyEntityResult<List<Asset>>>
{
    public BulkPatch<IdentifierOnly> HydraBulkPatch { get; } = hydraBulkPatch;

    public int CustomerId { get; set; } = customerId;
}

public class UpdateAllImagesHandler(IBulkAssetPatcher assetUpdater, ILogger<UpdateAllImagesHandler> logger)
    : IRequestHandler<UpdateAllImages, ModifyEntityResult<List<Asset>>>
{
    public async Task<ModifyEntityResult<List<Asset>>> Handle(UpdateAllImages request, CancellationToken cancellationToken)
    {
        try
        {
            var assets = await assetUpdater.UpdateAssets(request, cancellationToken);

            return ModifyEntityResult<List<Asset>>.Success(assets);
        }
        catch (InvalidOperationException e)
        {
            logger.LogError(e, "Failed to update assets {Assets}", request.HydraBulkPatch.Members);
            return ModifyEntityResult<List<Asset>>.Failure(e.Message, WriteResult.BadRequest);
        }
    }
}
