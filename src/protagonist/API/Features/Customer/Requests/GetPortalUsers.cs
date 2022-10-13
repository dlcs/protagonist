using System.Collections.Generic;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Customer.Requests;

/// <summary>
/// Get a list of all portal users for customer
/// </summary>
public class GetPortalUsers : IRequest<IList<User>>
{
    public int CustomerId { get; }

    public GetPortalUsers(int customerId)
    {
        CustomerId = customerId;
    }
}

public class GetPortalUsersHandler : IRequestHandler<GetPortalUsers, IList<User>>
{
    private readonly DlcsContext dbContext;

    public GetPortalUsersHandler(DlcsContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IList<User>> Handle(GetPortalUsers request, CancellationToken cancellationToken)
    {
        var users = await dbContext.Users.AsNoTracking()
            .Where(u => u.Customer == request.CustomerId)
            .ToListAsync(cancellationToken);
        return users;
    }
}