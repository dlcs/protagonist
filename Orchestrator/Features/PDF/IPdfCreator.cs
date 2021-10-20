using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using Orchestrator.Infrastructure.NamedQueries;

namespace Orchestrator.Features.PDF
{
    /// <summary>
    /// Basic interface for generation of PDF file
    /// </summary>
    public interface IPdfCreator
    {
        /// <summary>
        /// Generate PDF file from results contained in specified named query.
        /// </summary>
        /// <param name="namedQueryResult">Parsed named query and matching images</param>
        /// <returns>boolean representing success of creation</returns>
        Task<bool> CreatePdf(NamedQueryResult<PdfParsedNamedQuery> namedQueryResult);
    }
}