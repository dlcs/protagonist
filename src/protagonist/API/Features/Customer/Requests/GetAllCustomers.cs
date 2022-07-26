using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Customer.Requests;

/// <summary>
/// Mediatr command that will return all customers
/// </summary>
public class GetAllCustomers : IRequest<List<DLCS.Model.Customers.Customer>>
{
    
}

/// <summary>
/// Mediatr Handler - uses DB context directly rather than going to Customer repository
/// </summary>
public class GetAllCustomersHandler : IRequestHandler<GetAllCustomers, List<DLCS.Model.Customers.Customer>>
{
    private readonly DlcsContext dbContext;
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="dbContext"></param>
    public GetAllCustomersHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<List<DLCS.Model.Customers.Customer>> Handle(GetAllCustomers request, CancellationToken cancellationToken)
    {
        return await dbContext.Customers
            .OrderBy(c => c.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken);
    }
}