using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Model.Page;
using DLCS.Repository;
using DLCS.Repository.Assets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Space.Requests;

/// <summary>
/// Get a paged results of images in specified space.
/// </summary>
public class GetSpaceImages(int spaceId, int customerId, AssetQueryModel assetQueryModel)
    : IRequest<FetchEntityResult<PageOf<Asset>>>,
        IPagedRequest,
        IOrderableRequest,
        IAssetFilterableRequest
{
    public int SpaceId { get; } = spaceId;
    public int CustomerId { get; } = customerId;
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? Field { get; set; }
    public bool Descending { get; set; }

    public AssetQueryModel AssetQueryModel { get; } = assetQueryModel;
}

public class GetSpaceImagesHandler(DlcsContext dlcsContext) : IRequestHandler<GetSpaceImages, FetchEntityResult<PageOf<Asset>>>
{
    public async Task<FetchEntityResult<PageOf<Asset>>> Handle(GetSpaceImages request, CancellationToken cancellationToken)
    {
        var result = await dlcsContext.Images.AsNoTracking().CreatePagedResult(
            request,
            i => i
                .Where(a => a.Customer == request.CustomerId && a.Space == request.SpaceId)
                .IncludeRelated(request.AssetQueryModel.Include)
                .ApplyAssetFilter(request.AssetQueryModel.Filter)
                .IncludeDeliveryChannelsWithPolicy()
                .AsSingleQuery(),
            images => images.AsOrderedAssetQuery(request),
            cancellationToken);
        
        // Any empty result set could be the result of an applied asset filter - check if space exists
        if (result.Total == 0 && !await DoesSpaceExist(request, cancellationToken))
        {
            return FetchEntityResult<PageOf<Asset>>.NotFound();
        }

        return FetchEntityResult<PageOf<Asset>>.Success(result);
    }
    
    private async Task<bool> DoesSpaceExist(GetSpaceImages request, CancellationToken cancellationToken)
    {
        var spaceExists = await dlcsContext.Spaces.AsNoTracking()
            .AnyAsync(b => b.Customer == request.CustomerId && b.Id == request.SpaceId, cancellationToken);
        return spaceExists;
    }
}
