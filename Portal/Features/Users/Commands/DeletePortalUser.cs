using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Portal.Behaviours;
using Portal.Legacy;

namespace Portal.Features.Users.Commands
{
    /// <summary>
    /// Delete Portal user by Id
    /// </summary>
    public class DeletePortalUser : IRequest<bool>, IAuditable
    {
        public string UserId { get; }

        public DeletePortalUser(string userId)
        {
            UserId = userId;
        }
    }
    
    public class DeletePortalUserHandler : IRequestHandler<DeletePortalUser, bool>
    {
        private readonly DlcsClient dlcsClient;

        public DeletePortalUserHandler(DlcsClient dlcsClient)
        {
            this.dlcsClient = dlcsClient;
        }
        
        public Task<bool> Handle(DeletePortalUser request, CancellationToken cancellationToken)
        {
            return dlcsClient.DeletePortalUser(request.UserId);
        }
    }
}