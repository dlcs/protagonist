using System.Threading;
using System.Threading.Tasks;
using API.JsonLd;
using MediatR;
using Portal.Legacy;

namespace Portal.Features.Users.Requests
{
    /// <summary>
    /// Get all PortalUsers for current customer
    /// </summary>
    public class GetPortalUsers : IRequest<SimpleCollection<PortalUser>?>
    {
    }
    
    public class GetPortalUsersHandler : IRequestHandler<GetPortalUsers, SimpleCollection<PortalUser>?>
    {
        private readonly DlcsClient dlcsClient;

        public GetPortalUsersHandler(DlcsClient dlcsClient)
        {
            this.dlcsClient = dlcsClient;
        }
        
        public Task<SimpleCollection<PortalUser>?> Handle(GetPortalUsers request, CancellationToken cancellationToken)
        {
            var portalUsers = dlcsClient.GetPortalUsers();
            return portalUsers;
        }
    }
}