using System.Threading;
using System.Threading.Tasks;
using API.Client;
using MediatR;

namespace Portal.Features.NamedQueries.Requests;

/// <summary>
/// Delete a specified named query belonging to the current customer
/// </summary>
public class DeleteNamedQuery: IRequest<bool>
{
    public string NamedQueryId { get; set; }
}

public class DeleteNamedQueryHandler : IRequestHandler<DeleteNamedQuery, bool>
{
    private readonly IDlcsClient dlcsClient;

    public DeleteNamedQueryHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }
    
    public async Task<bool> Handle(DeleteNamedQuery request, CancellationToken cancellationToken)
    {
        return await dlcsClient.DeleteNamedQuery(request.NamedQueryId);
    }
}