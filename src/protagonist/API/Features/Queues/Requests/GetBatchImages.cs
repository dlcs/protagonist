using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Model.Page;
using DLCS.Repository;
using DLCS.Repository.Assets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Queues.Requests;

/// <summary>
/// Get details of images within batch
/// </summary>
public class GetBatchImages : IRequest<FetchEntityResult<PageOf<Asset>>>, IPagedRequest, IOrderableRequest, IAssetFilterableRequest
{
    public int CustomerId { get; }
    
    public int BatchId { get; }
    
    public AssetFilter? AssetFilter { get; }

    public int Page { get; set; }
    
    public int PageSize { get; set; }

    public string? Field { get; set; }

    public bool Descending { get; set; }

    public GetBatchImages(int customerId, int batchId, AssetFilter? assetFilter)
    {
        CustomerId = customerId;
        BatchId = batchId;
        AssetFilter = assetFilter;
    }
}

public class GetBatchImagesHandler : IRequestHandler<GetBatchImages, FetchEntityResult<PageOf<Asset>>>
{
    private readonly DlcsContext dlcsContext;

    public GetBatchImagesHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }
    
    public async Task<FetchEntityResult<PageOf<Asset>>> Handle(GetBatchImages request, CancellationToken cancellationToken)
    {
        var result = await dlcsContext.Images.AsNoTracking().CreatePagedResult(
            request,
            i => i
                .Where(a => a.Customer == request.CustomerId && a.Batch == request.BatchId)
                .ApplyAssetFilter(request.AssetFilter, true)
                .IncludeDeliveryChannelsWithPolicy()
                .AsSplitQuery(),
            images => images.AsOrderedAssetQuery(request),
            cancellationToken);

        // Any empty result set could be the result of an applied asset filter - check if batch exists
        if (result.Total == 0 && !await DoesBatchExist(request, cancellationToken))
        {
            return FetchEntityResult<PageOf<Asset>>.NotFound();
        }

        return FetchEntityResult<PageOf<Asset>>.Success(result);
    }

    private async Task<bool> DoesBatchExist(GetBatchImages request, CancellationToken cancellationToken)
    {
        var batchExists = await dlcsContext.Batches.AsNoTracking()
            .AnyAsync(b => b.Customer == request.CustomerId && b.Id == request.BatchId, cancellationToken);
        return batchExists;
    }
}