using System.Security.Authentication;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Portal.Features.Admin.Requests
{
    public class DeleteSignupLink : IRequest
    {
        public string Id { get; set; }
    }

    public class DeleteSignupLinkHandler : IRequestHandler<DeleteSignupLink>
    {
        private readonly DlcsContext dbContext;
        private readonly ClaimsPrincipal principal;
        private readonly ILogger logger;
        
        public DeleteSignupLinkHandler(
            DlcsContext dbContext, 
            ClaimsPrincipal principal,
            ILogger<DeleteSignupLinkHandler> logger)
        {
            this.dbContext = dbContext;
            this.principal = principal;
            this.logger = logger;
        }
        
        public async Task<Unit> Handle(DeleteSignupLink request, CancellationToken cancellationToken)
        {
            if (principal.IsAdmin())
            {
                var forDeletion = new SignupLink {Id = request.Id};
                dbContext.Remove(forDeletion);
                await dbContext.SaveChangesAsync(cancellationToken);
                return Unit.Value;
            }

            throw new InvalidCredentialException("Non admin user cannot delete signup links");
        }
    }
}