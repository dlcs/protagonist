using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using DLCS.Repository.NamedQueries;
using MediatR;

namespace API.Features.NamedQueries.Requests;

/// <summary>
/// Handler for deleting PDF for named query
/// </summary>
public class DeletePdf : IRequest<bool?>
{
    public int CustomerId { get; }
    public string NamedQuery { get; }
    public string? NamedQueryArgs { get; }

    public DeletePdf(int customerId, string namedQuery, string? namedQueryArgs)
    {
        CustomerId = customerId;
        NamedQuery = namedQuery;
        NamedQueryArgs = namedQueryArgs;
    }
}


public class DeletePdfHandler : IRequestHandler<DeletePdf, bool?>
{
    private readonly NamedQueryConductor namedQueryConductor;

    public DeletePdfHandler(NamedQueryConductor namedQueryConductor)
    {
        this.namedQueryConductor = namedQueryConductor;
    }
    
    public async Task<bool?> Handle(DeletePdf request, CancellationToken cancellationToken)
    {
        var pathElement = new IdOnlyPathElement(request.CustomerId);
        
        var namedQueryResult =
            await namedQueryConductor.GetNamedQueryResult<PdfParsedNamedQuery>(request.NamedQuery, pathElement,
                request.NamedQueryArgs);
        
        // Throw exception here?
        if (namedQueryResult.ParsedQuery is null or { IsFaulty: true }) return null;

        var toDelete = namedQueryResult.ParsedQuery.StorageKey;
    }
}