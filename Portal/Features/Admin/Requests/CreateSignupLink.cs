using System;
using System.Security.Authentication;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.Core.Encryption;
using DLCS.Model.Customers;
using DLCS.Repository;
using DLCS.Web.Auth;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Portal.Features.Admin.Requests
{
    public class CreateSignupLink : IRequest<SignupLink>
    {
        public string? Note { get; set; }
        public DateTime Expires { get; set; }
    }

    public class CreateSignupLinkHandler : IRequestHandler<CreateSignupLink, SignupLink>
    {
        private readonly DlcsContext dbContext;
        private readonly ClaimsPrincipal principal;
        private readonly ILogger logger;
        
        public CreateSignupLinkHandler(
            DlcsContext dbContext, 
            ClaimsPrincipal principal,
            ILogger<CreateSignupLinkHandler> logger)
        {
            this.dbContext = dbContext;
            this.principal = principal;
            this.logger = logger;
        }
        
        public async Task<SignupLink> Handle(CreateSignupLink request, CancellationToken cancellationToken)
        {
            if (principal.IsAdmin())
            {
                var newLink = new SignupLink
                {
                    Id = KeyGenerator.GetUniqueKey(24),
                    Created = DateTime.Now,
                    Expires = request.Expires,
                    Note = request.Note
                };
                dbContext.SignupLinks.Add(newLink);
                await dbContext.SaveChangesAsync(cancellationToken);
                return newLink;
            }

            throw new InvalidCredentialException("Only admin can create a signup link");
        }
    }
}