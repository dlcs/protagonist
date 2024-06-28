using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using Hydra.Collections;
using MediatR;

namespace Portal.Features.NamedQueries.Requests;

/// <summary>
/// Get all named queries belonging to the current customer
/// </summary>
public class GetCustomerNamedQueries: IRequest<HydraCollection<NamedQuery>>
{
}

public class GetCustomerNamedQueriesHandler : IRequestHandler<GetCustomerNamedQueries, HydraCollection<NamedQuery>>
{
    private readonly IDlcsClient dlcsClient;

    public GetCustomerNamedQueriesHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }
    
    public async Task<HydraCollection<NamedQuery>> Handle(GetCustomerNamedQueries request, CancellationToken cancellationToken)
    {
        var namedQueries = await dlcsClient.GetNamedQueries();
        return namedQueries;
    }
}