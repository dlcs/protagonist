using System.Collections.Generic;
using API.Converters;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model;
using DLCS.Model.Assets;
using Hydra.Collections;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Customer.Requests;

public class UpdateAllImages : IRequest<ModifyEntityResult<List<Asset>>>
{
    public UpdateAllImages(HydraUpdate<IdentifierOnly> hydraUpdate, int customerId)
    {
        HydraUpdate = hydraUpdate;
        CustomerId = customerId;
    }
    
    public HydraUpdate<IdentifierOnly> HydraUpdate { get; }
    
    public int CustomerId { get; set; }
}

public class UpdateAllImagesHandler(IAssetUpdater assetUpdater, ILogger<UpdateAllImagesHandler> logger)
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
            logger.LogError(e, "Failed to update assets {Assets}", request.HydraUpdate.Members);
            return ModifyEntityResult<List<Asset>>.Failure(e.Message, WriteResult.BadRequest);
        }
    }
}
