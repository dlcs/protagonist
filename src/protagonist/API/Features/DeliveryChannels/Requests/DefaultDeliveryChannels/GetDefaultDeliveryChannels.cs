using API.Infrastructure.Requests;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.Page;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DeliveryChannels.Requests.DefaultDeliveryChannels;

public class GetDefaultDeliveryChannels: IRequest<FetchEntityResult<PageOf<DefaultDeliveryChannel>>>, IPagedRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Customer { get; }
    public int Space { get; }
    
    public GetDefaultDeliveryChannels(int customer, int space)
    {
        Customer = customer;
        Space = space;
    }
}

public class GetDefaultDeliveryChannelsHandler : IRequestHandler<GetDefaultDeliveryChannels,
    FetchEntityResult<PageOf<DefaultDeliveryChannel>>>
{
    private readonly DlcsContext dlcsContext;

    public GetDefaultDeliveryChannelsHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<PageOf<DefaultDeliveryChannel>>> Handle(GetDefaultDeliveryChannels request,
        CancellationToken cancellationToken)
    {
        var filter = GetFilterForRequest(request);

        var result = await dlcsContext.DefaultDeliveryChannels.AsNoTracking().CreatePagedResult(request,
            filter,
            q => q.OrderBy(i => i.MediaType),
            cancellationToken: cancellationToken);

        return FetchEntityResult<PageOf<DefaultDeliveryChannel>>.Success(result);
    }

    private static Func<IQueryable<DefaultDeliveryChannel>, IQueryable<DefaultDeliveryChannel>> GetFilterForRequest(GetDefaultDeliveryChannels request)
    {
        return defaultDeliveryChannels => defaultDeliveryChannels
            .Include(d => d.DeliveryChannelPolicy)
            .Where(d => d.Customer == request.Customer && 
                        d.Space == request.Space);
    }
}
