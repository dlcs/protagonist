using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using MediatR;

namespace Portal.Features.NamedQueries.Requests;

/// <summary>
/// Retrieves all named queries belonging to the current customer
/// </summary>
public class GetCustomerNamedQueries: IRequest<IEnumerable<NamedQuery>>
{
}

public class GetCustomerNamedQueriesHandler : IRequestHandler<GetCustomerNamedQueries, IEnumerable<NamedQuery>>
{
    private readonly IDlcsClient dlcsClient;

    public GetCustomerNamedQueriesHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }
    
    public async Task<IEnumerable<NamedQuery>> Handle(GetCustomerNamedQueries request, CancellationToken cancellationToken)
    {
        var namedQueries = await dlcsClient.GetNamedQueries(false);
        return namedQueries;
    }
}