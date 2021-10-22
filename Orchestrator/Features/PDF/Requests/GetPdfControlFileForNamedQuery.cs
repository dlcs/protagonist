using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using MediatR;
using Orchestrator.Infrastructure.NamedQueries.Requests;

namespace Orchestrator.Features.PDF.Requests
{
    /// <summary>
    /// Mediatr request for getting PDF control-file for named query
    /// </summary>
    public class GetPdfControlFileForNamedQuery : IBaseNamedQueryRequest, IRequest<PdfControlFile?>
    {
        public string CustomerPathValue { get; }

        public string NamedQuery { get; }

        public string? NamedQueryArgs { get; }

        public GetPdfControlFileForNamedQuery(string customerPathValue, string namedQuery, string? namedQueryArgs)
        {
            CustomerPathValue = customerPathValue;
            NamedQuery = namedQuery;
            NamedQueryArgs = namedQueryArgs;
        }
    }
    
    public class GetPdfControlFileForNamedQueryHandler : IRequestHandler<GetPdfControlFileForNamedQuery, PdfControlFile?>
    {
        private readonly PdfNamedQueryService pdfNamedQueryService;
        private readonly NamedQueryResultGenerator namedQueryResultGenerator;

        public GetPdfControlFileForNamedQueryHandler(
            PdfNamedQueryService pdfNamedQueryService,
            NamedQueryResultGenerator namedQueryResultGenerator)
        {
            this.pdfNamedQueryService = pdfNamedQueryService;
            this.namedQueryResultGenerator = namedQueryResultGenerator;
        }
        
        public async Task<PdfControlFile?> Handle(GetPdfControlFileForNamedQuery request, CancellationToken cancellationToken)
        {
            var namedQueryResult = await namedQueryResultGenerator.GetNamedQueryResult<PdfParsedNamedQuery>(request);

            if (namedQueryResult.ParsedQuery is null or { IsFaulty: true }) return null;

            var pdfControlFile =
                await pdfNamedQueryService.GetPdfControlFile(namedQueryResult.ParsedQuery.ControlFileStorageKey);
            return pdfControlFile;
        }
    }
}