using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using Orchestrator.Infrastructure.NamedQueries;

namespace Orchestrator.Features.PDF
{
    public interface IPdfCreator
    {
        Task<bool> CreatePdf(NamedQueryResult<PdfParsedNamedQuery> namedQueryResult, string queryName);
    }
}