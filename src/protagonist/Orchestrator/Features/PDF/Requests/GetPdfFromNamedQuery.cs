using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using MediatR;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Infrastructure.NamedQueries.Requests;

namespace Orchestrator.Features.PDF.Requests;

/// <summary>
/// Mediatr request for generating PDF via named query
/// </summary>
public class GetPdfFromNamedQuery : IBaseNamedQueryRequest, IRequest<PersistedNamedQueryProjection>
{
    public string CustomerPathValue { get; }
    public string NamedQuery { get; }
    public string? NamedQueryArgs { get; }
    
    public GetPdfFromNamedQuery(string customerPathValue, string namedQuery, string? namedQueryArgs)
    {
        CustomerPathValue = customerPathValue;
        NamedQuery = namedQuery;
        NamedQueryArgs = namedQueryArgs;
    }
}

public class GetPdfFromNamedQueryHandler : IRequestHandler<GetPdfFromNamedQuery, PersistedNamedQueryProjection>
{
    private readonly StoredNamedQueryService storedNamedQueryService;
    private readonly NamedQueryResultGenerator namedQueryResultGenerator;
    private readonly IProjectionCreator<PdfParsedNamedQuery> pdfCreator;

    public GetPdfFromNamedQueryHandler(
        StoredNamedQueryService storedNamedQueryService,
        NamedQueryResultGenerator namedQueryResultGenerator,
        IProjectionCreator<PdfParsedNamedQuery> pdfCreator)
    {
        this.storedNamedQueryService = storedNamedQueryService;
        this.namedQueryResultGenerator = namedQueryResultGenerator;
        this.pdfCreator = pdfCreator;
    }

    public async Task<PersistedNamedQueryProjection> Handle(GetPdfFromNamedQuery request,
        CancellationToken cancellationToken)
    {
        var namedQueryResult =
            await namedQueryResultGenerator.GetNamedQueryResult<PdfParsedNamedQuery>(request);

        if (namedQueryResult.ParsedQuery == null)
            return new PersistedNamedQueryProjection(PersistedProjectionStatus.NotFound);
        if (namedQueryResult.ParsedQuery is { IsFaulty: true })
            return PersistedNamedQueryProjection.BadRequest();

        var pdfResult =
            await storedNamedQueryService.GetResults(namedQueryResult, pdfCreator, true, cancellationToken);

        return pdfResult.Status is PersistedProjectionStatus.InProcess or PersistedProjectionStatus.Restricted
            ? new PersistedNamedQueryProjection(pdfResult.Status)
            : new PersistedNamedQueryProjection(pdfResult.Stream, pdfResult.Status,
                pdfResult.RequiresAuth ?? false);
    }
}