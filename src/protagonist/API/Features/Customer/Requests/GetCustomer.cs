using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Customer.Requests;

/// <summary>
/// Mediatr command to Get a Customer
/// </summary>
/// <remarks>This does not go via the customer repository and speaks to DBContext directly</remarks>
public class GetCustomer : IRequest<DLCS.Model.Customers.Customer?>
{
    /// <summary>
    /// Integer form of Customer ID
    /// </summary>
    public int CustomerId { get; }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    public GetCustomer(int id)
    {
        CustomerId = id;
    }
}

public class GetCustomerHandler : IRequestHandler<GetCustomer, DLCS.Model.Customers.Customer?>
{
    private readonly DlcsContext dbContext;
    
    public GetCustomerHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<DLCS.Model.Customers.Customer?> Handle(GetCustomer request, CancellationToken cancellationToken)
    {
        return await dbContext.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);
    }
}