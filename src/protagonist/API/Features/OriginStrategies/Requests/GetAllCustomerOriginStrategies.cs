using System.Collections.Generic;
using API.Infrastructure.Requests;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.OriginStrategies.Requests;

public class GetAllCustomerOriginStrategies : IRequest<FetchEntityResult<IReadOnlyCollection<CustomerOriginStrategy>>>
{
    public int CustomerId { get; }
    
    public GetAllCustomerOriginStrategies(int id)
    {
        CustomerId = id;
    }
}

public class GetAllCustomerOriginStrategiesHandler : IRequestHandler<GetAllCustomerOriginStrategies, FetchEntityResult<IReadOnlyCollection<CustomerOriginStrategy>>>
{
    private readonly DlcsContext dbContext;
    
    public GetAllCustomerOriginStrategiesHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }
    
    public async Task<FetchEntityResult<IReadOnlyCollection<CustomerOriginStrategy>>> Handle(
        GetAllCustomerOriginStrategies request, 
        CancellationToken cancellationToken)
    {
        var strategies = await dbContext.CustomerOriginStrategies
            .AsNoTracking()
            .Where(s => s.Customer == request.CustomerId)
            .OrderBy(s => s.Order)
            .ToListAsync(cancellationToken);
       
        // Hide credentials in returned JSON object 
        foreach(var strategy in strategies) 
            strategy.Credentials = "xxx";
        
        return FetchEntityResult<IReadOnlyCollection<CustomerOriginStrategy>>.Success(strategies);
    }
}