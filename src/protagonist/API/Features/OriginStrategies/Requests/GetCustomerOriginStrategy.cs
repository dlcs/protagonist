using API.Infrastructure.Requests;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.OriginStrategies.Requests;

public class GetCustomerOriginStrategy : IRequest<FetchEntityResult<CustomerOriginStrategy>>
{
    public int CustomerId { get; }
    
    public string StrategyId { get; }
    
    public GetCustomerOriginStrategy(int id, string namedQueryId)
    {
        CustomerId = id;
        StrategyId = namedQueryId;
    }
}

public class GetCustomerOriginStrategyHandler : IRequestHandler<GetCustomerOriginStrategy, FetchEntityResult<CustomerOriginStrategy>>
{
    private readonly DlcsContext dbContext;
    
    public GetCustomerOriginStrategyHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<FetchEntityResult<CustomerOriginStrategy>> Handle(GetCustomerOriginStrategy request, CancellationToken cancellationToken)
    {
        var strategy = await dbContext.CustomerOriginStrategies
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Customer == request.CustomerId
                                        && s.Id == request.StrategyId, cancellationToken);
        return strategy == null
            ? FetchEntityResult<CustomerOriginStrategy>.NotFound()
            : FetchEntityResult<CustomerOriginStrategy>.Success(strategy);
    }
}