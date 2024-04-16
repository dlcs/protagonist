using System.Collections.Generic;
using API.Features.Customer.Validation;
using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Assets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Customer.Requests;

/// <summary>
/// Get a list of all images whose id is in ImageIds list
/// </summary>
public class GetMultipleImagesById : IRequest<FetchEntityResult<IReadOnlyCollection<Asset>>>
{
    public IReadOnlyCollection<string> AssetIds { get; }
    public int CustomerId { get; }

    public GetMultipleImagesById(IReadOnlyCollection<string> assetIds, int customerId)
    {
        AssetIds = assetIds;
        CustomerId = customerId;
    }
}

public class GetMultipleImagesByIdHandler 
    : IRequestHandler<GetMultipleImagesById, FetchEntityResult<IReadOnlyCollection<Asset>>>
{
    private readonly DlcsContext dlcsContext;

    public GetMultipleImagesByIdHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<IReadOnlyCollection<Asset>>> Handle(GetMultipleImagesById request,
        CancellationToken cancellationToken)
    {
        var assetIds = ImageIdListValidation.ValidateRequest(request.AssetIds, request.CustomerId);

        var results = await dlcsContext.Images.AsNoTracking()
            .Where(i => i.Customer == request.CustomerId && assetIds.Contains(i.Id))
            .IncludeDeliveryChannelsWithPolicy()
            .ToListAsync(cancellationToken);

        return FetchEntityResult<IReadOnlyCollection<Asset>>.Success(results);
    }
}