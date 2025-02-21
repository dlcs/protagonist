using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using Hydra.Collections;
using MediatR;

namespace Portal.Features.Users.Requests;

/// <summary>
/// Get all PortalUsers for current customer
/// </summary>
public class GetPortalUsers : IRequest<HydraCollection<PortalUser>?>
{
}

public class GetPortalUsersHandler : IRequestHandler<GetPortalUsers, HydraCollection<PortalUser>?>
{
    private readonly IDlcsClient dlcsClient;

    public GetPortalUsersHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }
    
    public Task<HydraCollection<PortalUser>?> Handle(GetPortalUsers request, CancellationToken cancellationToken)
    {
        var portalUsers = dlcsClient.GetPortalUsers();
        return portalUsers;
    }
}