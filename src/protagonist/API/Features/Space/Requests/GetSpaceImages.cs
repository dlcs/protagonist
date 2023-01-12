using System.Security.Claims;
using API.Exceptions;
using API.Infrastructure.Requests;
using DLCS.Model.Assets;
using DLCS.Model.Page;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Web.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Space.Requests;

/// <summary>
/// Get a paged results of images in specified space.
/// </summary>
public class GetSpaceImages : 
    IRequest<FetchEntityResult<PageOf<Asset>>>, 
    IPagedRequest, 
    IOrderableRequest, 
    IAssetFilterableRequest
{
    public GetSpaceImages(int spaceId, int? customerId = null, AssetFilter? assetFilter = null)
    {
        CustomerId = customerId;
        SpaceId = spaceId;
        AssetFilter = assetFilter;
    }
    
    public int SpaceId { get; }
    public int? CustomerId { get; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? Field { get; set; }
    public bool Descending { get; set; }
    
    public AssetFilter? AssetFilter { get; }
}

public class GetSpaceImagesHandler : IRequestHandler<GetSpaceImages, FetchEntityResult<PageOf<Asset>>>
{
    private readonly ClaimsPrincipal principal;
    private readonly DlcsContext dlcsContext;
    
    public GetSpaceImagesHandler(
        ClaimsPrincipal principal,
        DlcsContext dlcsContext)
    {
        this.principal = principal;
        this.dlcsContext = dlcsContext;
    }
    
    public async Task<FetchEntityResult<PageOf<Asset>>> Handle(GetSpaceImages request, CancellationToken cancellationToken)
    {
        int? customerId = request.CustomerId ?? principal.GetCustomerId();
        if (customerId == null)
        {
            throw new BadRequestException("No customer Id supplied");
        }
        
        var result = await dlcsContext.Images.AsNoTracking().CreatePagedResult(
            request,
            i => i
                .Where(a => a.Customer == request.CustomerId && a.Space == request.SpaceId)
                .ApplyAssetFilter(request.AssetFilter),
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