using System.Threading;
using System.Threading.Tasks;
using API.Client;
using API.Client.JsonLd;
using MediatR;

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
        private readonly IDlcsClient dlcsClient;

        public GetPortalUsersHandler(IDlcsClient dlcsClient)
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