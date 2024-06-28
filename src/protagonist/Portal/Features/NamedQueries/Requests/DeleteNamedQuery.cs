using System.Threading;
using System.Threading.Tasks;
using API.Client;
using MediatR;

namespace Portal.Features.NamedQueries.Requests;

/// <summary>
/// Delete a specified named query belonging to the current customer
/// </summary>
public class DeleteNamedQuery: IRequest
{
    public string NamedQueryId { get; set; }
}

public class DeleteNamedQueryHandler : IRequestHandler<DeleteNamedQuery>
{
    private readonly IDlcsClient dlcsClient;

    public DeleteNamedQueryHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }
    
    public async Task<Unit> Handle(DeleteNamedQuery request, CancellationToken cancellationToken)
    {
        await dlcsClient.DeleteNamedQuery(request.NamedQueryId);
        return Unit.Value;
    }
}