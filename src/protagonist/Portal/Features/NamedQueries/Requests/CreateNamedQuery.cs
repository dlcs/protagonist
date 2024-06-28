using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using MediatR;

namespace Portal.Features.NamedQueries.Requests;

/// <summary>
/// Create a new named query belonging to the customer
/// </summary>
public class CreateNamedQuery: IRequest<NamedQuery>
{
    public string Name { get; set; }
    
    public string Template { get; set; }
}

public class CreateNamedQueryHandler : IRequestHandler<CreateNamedQuery, NamedQuery>
{
    private readonly IDlcsClient dlcsClient;

    public CreateNamedQueryHandler(IDlcsClient dlcsClient)
    {
        this.dlcsClient = dlcsClient;
    }
    
    public async Task<NamedQuery> Handle(CreateNamedQuery request, CancellationToken cancellationToken)
    {
        return await dlcsClient.CreateNamedQuery(new NamedQuery() { Name = request.Name, Template = request.Template });
    }
}