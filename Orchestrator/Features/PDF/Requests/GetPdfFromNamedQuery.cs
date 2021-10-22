using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using MediatR;
using Orchestrator.Infrastructure.NamedQueries.Requests;

namespace Orchestrator.Features.PDF.Requests
{
    /// <summary>
    /// Mediatr request for generating PDF via named query
    /// </summary>
    public class GetPdfFromNamedQuery : IBaseNamedQueryRequest, IRequest<PdfFromNamedQuery>
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
    
    public class GetPdfFromNamedQueryHandler : IRequestHandler<GetPdfFromNamedQuery, PdfFromNamedQuery>
    {
        private readonly PdfNamedQueryService pdfNamedQueryService;
        private readonly NamedQueryResultGenerator namedQueryResultGenerator;

        public GetPdfFromNamedQueryHandler(
            PdfNamedQueryService pdfNamedQueryService,
            NamedQueryResultGenerator namedQueryResultGenerator)
        {
            this.pdfNamedQueryService = pdfNamedQueryService;
            this.namedQueryResultGenerator = namedQueryResultGenerator;
        }
        
        public async Task<PdfFromNamedQuery> Handle(GetPdfFromNamedQuery request, CancellationToken cancellationToken)
        {
            var namedQueryResult = await namedQueryResultGenerator.GetNamedQueryResult<PdfParsedNamedQuery>(request);

            if (namedQueryResult.ParsedQuery == null) return new PdfFromNamedQuery(PdfStatus.NotFound);
            if (namedQueryResult.ParsedQuery is { IsFaulty: true }) return PdfFromNamedQuery.BadRequest();

            var pdfResult = await pdfNamedQueryService.GetPdfResults(namedQueryResult);

            return pdfResult.Status == PdfStatus.InProcess
                ? new PdfFromNamedQuery(PdfStatus.InProcess)
                : new PdfFromNamedQuery(pdfResult.Stream, pdfResult.Status);
        }
    }

    /// <summary>
    /// Represents the result of a request to generate a PDF from NQ
    /// </summary>
    public class PdfFromNamedQuery
    {
        /// <summary>
        /// Stream containing PDF data.
        /// </summary>
        public Stream PdfStream { get; } = Stream.Null;

        /// <summary>
        /// Overall status of PDF request
        /// </summary>
        public PdfStatus Status { get; } = PdfStatus.Unknown;

        /// <summary>
        /// Whether this result object has data
        /// </summary>
        public bool IsEmpty => PdfStream == Stream.Null;
        
        /// <summary>
        /// Whether this request could not be satisfied as a result of a bad request
        /// </summary>
        public bool IsBadRequest { get; private init; }

        public static PdfFromNamedQuery BadRequest() => new() { IsBadRequest = true };
        
        public PdfFromNamedQuery()
        {
        }
        
        public PdfFromNamedQuery(PdfStatus status)
        {
            PdfStream = Stream.Null;
            Status = status;
        }

        public PdfFromNamedQuery(Stream? pdfStream, PdfStatus status)
        {
            PdfStream = pdfStream ?? Stream.Null;
            Status = status;
        }
    }
}