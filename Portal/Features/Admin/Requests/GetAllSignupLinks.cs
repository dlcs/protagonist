using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portal.Features.Account.Models;

namespace Portal.Features.Admin.Requests
{
    /// <summary>
    /// Request to get all available signup links (whether used or not)
    /// </summary>
    public class GetAllSignupLinks : IRequest<List<SignupModel>>
    {
        
    }

    public class GetAllSignupLinksHandler : IRequestHandler<GetAllSignupLinks, List<SignupModel>>
    {
        private readonly DlcsContext dbContext;
        private readonly ClaimsPrincipal principal;
        private readonly ILogger logger;
        
        public GetAllSignupLinksHandler(
            DlcsContext dbContext, 
            ClaimsPrincipal principal,
            ILogger<GetAllSignupLinksHandler> logger)
        {
            this.dbContext = dbContext;
            this.principal = principal;
            this.logger = logger;
        }

        public async Task<List<SignupModel>> Handle(GetAllSignupLinks request, CancellationToken cancellationToken)
        {
            if (principal.IsAdmin())
            {
                var query =
                    from link in dbContext.SignupLinks
                    join customer in dbContext.Customers
                        on link.CustomerId equals customer.Id into grouping
                    from customer in grouping.DefaultIfEmpty()
                    select new {link, customer};
                var signups = await query.ToListAsync(cancellationToken);
                return signups.Select(signup => new SignupModel
                {
                    Id = signup.link.Id,
                    Created = signup.link.Created,
                    Expires = signup.link.Expires,
                    Note = signup.link.Note,
                    CustomerName = signup.customer?.Name,
                    CustomerId = signup.customer?.Id
                }).ToList();
            }
            throw new InvalidCredentialException("Must be admin to see signups");
        }
    }
}