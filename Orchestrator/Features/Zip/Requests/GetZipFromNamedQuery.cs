using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using MediatR;
using Orchestrator.Features.PDF;
using Orchestrator.Infrastructure.NamedQueries.Models;
using Orchestrator.Infrastructure.NamedQueries.Requests;

namespace Orchestrator.Features.Zip.Requests
{
    public class GetZipFromNamedQuery : IBaseNamedQueryRequest, IRequest<PersistedProjectionFromNamedQuery>
    {
        public string CustomerPathValue { get; }
        public string NamedQuery { get; }
        public string? NamedQueryArgs { get; }
        
        public GetZipFromNamedQuery(string customerPathValue, string namedQuery, string? namedQueryArgs)
        {
            CustomerPathValue = customerPathValue;
            NamedQuery = namedQuery;
            NamedQueryArgs = namedQueryArgs;
        }
    }
    
    public class GetPdfFromNamedQueryHandler : IRequestHandler<GetZipFromNamedQuery, PersistedProjectionFromNamedQuery>
    {
        private readonly StoredNamedQueryService storedNamedQueryService;
        private readonly NamedQueryResultGenerator namedQueryResultGenerator;

        public GetPdfFromNamedQueryHandler(
            StoredNamedQueryService storedNamedQueryService,
            NamedQueryResultGenerator namedQueryResultGenerator)
        {
            this.storedNamedQueryService = storedNamedQueryService;
            this.namedQueryResultGenerator = namedQueryResultGenerator;
        }
        
        public async Task<PersistedProjectionFromNamedQuery> Handle(GetZipFromNamedQuery request, CancellationToken cancellationToken)
        {
            var namedQueryResult =
                await namedQueryResultGenerator.GetNamedQueryResult<StoredParsedNamedQuery>(request);

            if (namedQueryResult.ParsedQuery == null)
                return new PersistedProjectionFromNamedQuery(PersistedProjectionStatus.NotFound);
            if (namedQueryResult.ParsedQuery is { IsFaulty: true })
                return PersistedProjectionFromNamedQuery.BadRequest();
            
            // Stream ZIP 
            var pdfResult = await storedNamedQueryService.GetResults(namedQueryResult,
                (query, images) => Task.FromResult(false)
                // TODO create ZIP
            );

            return pdfResult.Status == PersistedProjectionStatus.InProcess
                ? new PersistedProjectionFromNamedQuery(PersistedProjectionStatus.InProcess)
                : new PersistedProjectionFromNamedQuery(pdfResult.Stream, pdfResult.Status);
        }
    }
}