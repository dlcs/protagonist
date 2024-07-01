using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using MediatR;

namespace Portal.Features.NamedQueries.Requests;

/// <summary>
/// Update a specified named query belonging to the current customer
/// </summary>
public class UpdateNamedQuery: IRequest<NamedQuery>
{
    public string NamedQueryId { get; set; }
    
    public string Template { get; set; }
}

public class UpdateNamedQueryHandler : IRequestHandler<UpdateNamedQuery, NamedQuery>
{
    private readonly IDlcsClient dlcsClient;

    public UpdateNamedQueryHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }
    
    public async Task<NamedQuery> Handle(UpdateNamedQuery request, CancellationToken cancellationToken)
    {
        return await dlcsClient.UpdateNamedQuery(request.NamedQueryId, request.Template);
    }
}