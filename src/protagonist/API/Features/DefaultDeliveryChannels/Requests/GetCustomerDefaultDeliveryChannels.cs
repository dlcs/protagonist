using API.Infrastructure.Requests;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.Page;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.DefaultDeliveryChannels.Requests;

public class GetCustomerDefaultDeliveryChannels: IRequest<FetchEntityResult<PageOf<DefaultDeliveryChannel>>>, IPagedRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Customer { get; }
    
    public GetCustomerDefaultDeliveryChannels(int customer)
    {
        Customer = customer;
    }
}

public class GetCustomerDefaultDeliveryChannelsHandler : IRequestHandler<GetCustomerDefaultDeliveryChannels,
    FetchEntityResult<PageOf<DefaultDeliveryChannel>>>
{
    private readonly DlcsContext dlcsContext;

    public GetCustomerDefaultDeliveryChannelsHandler(DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public async Task<FetchEntityResult<PageOf<DefaultDeliveryChannel>>> Handle(GetCustomerDefaultDeliveryChannels request,
        CancellationToken cancellationToken)
    {
        var filter = GetFilterForRequest(request);

        var result = await dlcsContext.DefaultDeliveryChannels.AsNoTracking().CreatePagedResult(request,
            filter,
            q => q.OrderBy(i => i.Id),
            cancellationToken: cancellationToken);

        return FetchEntityResult<PageOf<DefaultDeliveryChannel>>.Success(result);
    }

    private static Func<IQueryable<DefaultDeliveryChannel>, IQueryable<DefaultDeliveryChannel>> GetFilterForRequest(GetCustomerDefaultDeliveryChannels request)
    {
        Func<IQueryable<DefaultDeliveryChannel>, IQueryable<DefaultDeliveryChannel>> filter =
             defaultDeliveryChannels => defaultDeliveryChannels.Include(d => d.DeliveryChannelPolicy)
                 .Where(d => d.Customer == request.Customer && 
                             d.Space == 0);
        return filter;
    }
}
