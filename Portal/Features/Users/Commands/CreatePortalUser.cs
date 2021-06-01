using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.JsonLd;
using MediatR;
using Microsoft.Extensions.Logging;
using Portal.Legacy;

namespace Portal.Features.Users.Commands
{
    /// <summary>
    /// Create a new portal user with specified username and password
    /// </summary>
    public class CreatePortalUser : IRequest<PortalUser?>
    {
        public string Email { get; set; }
        public string Password { get; set; }

        public CreatePortalUser(string email, string password)
        {
            Email = email;
            Password = password;
        }
    }
    
    public class CreatePortalUserHandler : IRequestHandler<CreatePortalUser, PortalUser?>
    {
        private readonly DlcsClient dlcsClient;
        private readonly ILogger<CreatePortalUserHandler> logger;
        private readonly ClaimsPrincipal claimsPrincipal;

        public CreatePortalUserHandler(
            DlcsClient dlcsClient, 
            ILogger<CreatePortalUserHandler> logger,
            ClaimsPrincipal claimsPrincipal)
        {
            this.dlcsClient = dlcsClient;
            this.logger = logger;
            this.claimsPrincipal = claimsPrincipal;
        }
        
        public async Task<PortalUser?> Handle(CreatePortalUser request, CancellationToken cancellationToken)
        {
            // TODO - validation
            var newPortalUser = new PortalUser
            {
                Email = request.Email,
                Password = request.Password,
                Enabled = true
            };
            try
            {
                var createdUser = await dlcsClient.CreatePortalUser(newPortalUser);

                logger.LogInformation("New Portal user '{PortalUser}' created by '{CurrentUser}'",
                    createdUser.GetLastPathElement(),
                    claimsPrincipal.GetUserId());
                return createdUser;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating new Portal User");
            }

            return null;
        }
    }
}