using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository;
using DLCS.Repository.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Portal.Features.Spaces.Requests
{
    public class GetAllSpaces : IRequest<IEnumerable<Space>>
    {
        
    }

    public class GetAllSpacesHandler : IRequestHandler<GetAllSpaces, IEnumerable<Space>>
    {
        private readonly DlcsContext dbContext;
        private readonly ClaimsPrincipal principal;
        private readonly ILogger logger;

        public GetAllSpacesHandler(DlcsContext dbContext, ClaimsPrincipal principal,
            ILogger<GetAllSpacesHandler> logger)
        {
            this.dbContext = dbContext;
            this.principal = principal;
            this.logger = logger;
        }

        public async Task<IEnumerable<Space>> Handle(GetAllSpaces request, CancellationToken cancellationToken)
        {
            var customerId = principal.GetCustomerId();
            // TODO - throw if null

            return dbContext.Spaces.AsNoTracking().Where(s => s.Customer == customerId);
        }
    }
}