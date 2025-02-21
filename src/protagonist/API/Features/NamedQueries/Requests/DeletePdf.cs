using DLCS.Model.Assets.NamedQueries;
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
    private readonly NamedQueryStorageService namedQueryStorageService;

    public DeletePdfHandler(NamedQueryConductor namedQueryConductor,
        NamedQueryStorageService namedQueryStorageService)
    {
        this.namedQueryConductor = namedQueryConductor;
        this.namedQueryStorageService = namedQueryStorageService;
    }

    public async Task<bool?> Handle(DeletePdf request, CancellationToken cancellationToken)
    {
        var namedQueryResult =
            await namedQueryConductor.GetNamedQueryResult<PdfParsedNamedQuery>(request.NamedQuery, request.CustomerId,
                request.NamedQueryArgs);
        
        if (namedQueryResult.ParsedQuery is null or { IsFaulty: true }) return null;

        var success = await namedQueryStorageService.DeleteStoredNamedQuery(namedQueryResult.ParsedQuery);
        return success;
    }
}