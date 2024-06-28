using System.Threading;
using System.Threading.Tasks;
using API.Client;
using MediatR;

namespace Portal.Features.NamedQueries.Requests;

/// <summary>
/// Update a specified named query belonging to the current customer
/// </summary>
public class UpdateNamedQuery: IRequest
{
    public string NamedQueryId { get; set; }
    
    public string Template { get; set; }
}

public class UpdateNamedQueryHandler : IRequestHandler<UpdateNamedQuery>
{
    private readonly IDlcsClient dlcsClient;

    public UpdateNamedQueryHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }
    
    public async Task<Unit> Handle(UpdateNamedQuery request, CancellationToken cancellationToken)
    {
        await dlcsClient.UpdateNamedQuery(request.NamedQueryId, request.Template);
        return Unit.Value;
    }
}