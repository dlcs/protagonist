using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Customers;
using DLCS.Repository;
using DLCS.Repository.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Portal.Features.Admin.Requests
{
    public class GetUsers : IRequest<List<User>>
    {
        public GetUsers(int customerId)
        {
            CustomerId = customerId;
        }

        public int CustomerId { get; set; }
    }

    public class GetUsersHandler : IRequestHandler<GetUsers, List<User>>
    {
        private readonly DlcsContext dbContext;
        private readonly ClaimsPrincipal principal;
        private readonly ILogger<GetUsersHandler> logger;

        public GetUsersHandler(
            DlcsContext dbContext,
            ClaimsPrincipal principal,
            ILogger<GetUsersHandler> logger)
        {
            this.dbContext = dbContext;
            this.principal = principal;
            this.logger = logger;
        }

        public async Task<List<User>> Handle(GetUsers request, CancellationToken cancellationToken)
        {
            return await dbContext.Users.AsNoTracking()
                .Where(u => u.Customer == request.CustomerId)
                .ToListAsync(cancellationToken: cancellationToken);
        }
    }
}