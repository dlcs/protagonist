using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using Orchestrator.Infrastructure.NamedQueries;

namespace Orchestrator.Features.PDF
{
    public class PdfNamedQueryService
    {
        public PdfNamedQueryService()
        {
        }
        
        public async Task<Stream?> GetPdfStream(NamedQueryResult<PdfParsedNamedQuery> namedQueryResult, CancellationToken cancellationToken)
        {
            // Check if control file exists.
            // If so, is it stale?
            // If not, return path to S3 (or bytes) 
        
            // If not, generate control file
            // call Fireball
            // update control file
            
            throw new System.NotImplementedException();
        }
    }
}