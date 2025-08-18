using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Model.Page;
using DLCS.Repository;
using DLCS.Repository.Assets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Customer.Requests;

/// <summary>
/// Retrieves all images that match supported query parameters
/// </summary>
public class GetQueriedAllImages(int customerId, AssetFilter? assetFilter) : IRequest<FetchEntityResult<PageOf<Asset>>>,
    IPagedRequest, IOrderableRequest,
    IAssetFilterableRequest
{
    public int CustomerId { get; } = customerId;

    public AssetFilter? AssetFilter { get; } = assetFilter;

    public int Page { get; set; }

    public int PageSize { get; set; }

    public string? Field { get; set; }

    public bool Descending { get; set; }
}

public class GetQueriedAllImagesHandler(DlcsContext dlcsContext)
    : IRequestHandler<GetQueriedAllImages, FetchEntityResult<PageOf<Asset>>>
{
    private DlcsContext dlcsContext { get; } = dlcsContext;

    public async Task<FetchEntityResult<PageOf<Asset>>> Handle(GetQueriedAllImages request, CancellationToken cancellationToken = default)
    {
        var result = await dlcsContext.Images
            .AsNoTracking()
            .IncludeDeliveryChannelsWithPolicy()
            .Where(a => a.Customer == request.CustomerId)
            .CreatePagedResult(
                request,
                i => i
                    .ApplyAssetFilter(request.AssetFilter)
                    .AsSingleQuery(),
                images => images.AsOrderedAssetQuery(request),
                cancellationToken);
        return  FetchEntityResult<PageOf<Asset>>.Success(result);
    }
}
