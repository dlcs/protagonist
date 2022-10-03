using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using API.Infrastructure.Requests;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Customer.Requests;

/// <summary>
/// Get a list of all images whose id is in ImageIds list
/// </summary>
public class GetMultipleImagesById : IRequest<FetchEntityResult<IReadOnlyCollection<Asset>>>
{
    public IReadOnlyCollection<string> ImageIds { get; }
    public int CustomerId { get; }

    public GetMultipleImagesById(IReadOnlyCollection<string> imageIds, int customerId)
    {
        ImageIds = imageIds;
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
        ValidateRequest(request);

        var results = await dlcsContext.Images.AsNoTracking()
            .Where(i => i.Customer == request.CustomerId && request.ImageIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        return FetchEntityResult<IReadOnlyCollection<Asset>>.Success(results);
    }

    private static void ValidateRequest(GetMultipleImagesById request)
    {
        IEnumerable<AssetId> assetIds;
        try
        {
            assetIds = request.ImageIds.Select(i => AssetId.FromString(i));
        }
        catch (FormatException formatException)
        {
            throw new BadRequestException(formatException.Message, formatException);
        }

        if (assetIds.Any(a => a.Customer != request.CustomerId))
        {
            throw new BadRequestException("Cannot request images for different customer");
        }
    }
}