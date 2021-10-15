using System;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using Destructurama.Attributed;
using DLCS.HydraModel;
using MediatR;
using Microsoft.Extensions.Logging;
using Portal.Behaviours;

namespace Portal.Features.Users.Requests
{
    /// <summary>
    /// Create a new portal user with specified username and password
    /// </summary>
    public class CreatePortalUser : IRequest<PortalUser?>, IAuditable
    {
        public string Email { get; }
        
        [NotLogged]
        public string Password { get; }

        public CreatePortalUser(string email, string password)
        {
            Email = email;
            Password = password;
        }
    }
    
    public class CreatePortalUserHandler : IRequestHandler<CreatePortalUser, PortalUser?>
    {
        private readonly IDlcsClient dlcsClient;
        private readonly ILogger<CreatePortalUserHandler> logger;

        public CreatePortalUserHandler(
            IDlcsClient dlcsClient, 
            ILogger<CreatePortalUserHandler> logger)
        {
            this.dlcsClient = dlcsClient;
            this.logger = logger;
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
                return createdUser;
            }
            catch (Exception ex)
            {
                // TODO - better handling of errors - return something better than null
                logger.LogError(ex, "Error creating new Portal User");
            }

            return null;
        }
    }
}