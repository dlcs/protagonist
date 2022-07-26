using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Portal.Features.Admin.Requests;

public class GetCustomer : IRequest<Customer>
{
    public int CustomerId { get; set; }
    public GetCustomer(int id)
    {
        CustomerId = id;
    }
}

public class GetCustomerHandler : IRequestHandler<GetCustomer, Customer>
{
    private readonly DlcsContext dbContext;
    private readonly ClaimsPrincipal principal;
    private readonly ILogger<GetCustomerHandler> logger;

    public GetCustomerHandler(
        DlcsContext dbContext, 
        ClaimsPrincipal principal,
        ILogger<GetCustomerHandler> logger)
    {
        this.dbContext = dbContext;
        this.principal = principal;
        this.logger = logger;
    }

    public async Task<Customer> Handle(GetCustomer request, CancellationToken cancellationToken)
    {
        return await dbContext.Customers.AsNoTracking().SingleAsync(c => c.Id == request.CustomerId);
    }
}