using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Portal.Features.Admin.Requests;

public class GetAllCustomers : IRequest<List<Customer>>
{
    
}

public class GetAllCustomersHandler : IRequestHandler<GetAllCustomers, List<Customer>>
{
    private readonly DlcsContext dbContext;
    private readonly ClaimsPrincipal principal;
    private readonly ILogger<GetAllCustomersHandler> logger;
    
    public GetAllCustomersHandler(
        DlcsContext dbContext, 
        ClaimsPrincipal principal,
        ILogger<GetAllCustomersHandler> logger)
    {
        this.dbContext = dbContext;
        this.principal = principal;
        this.logger = logger;
    }

    public async Task<List<Customer>> Handle(GetAllCustomers request, CancellationToken cancellationToken)
    {
        return await dbContext.Customers.AsNoTracking().ToListAsync(cancellationToken: cancellationToken);
    }
}