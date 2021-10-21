using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;

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
        /// <param name="parsedNamedQuery">Parsed named query</param>
        /// <param name="images">Matching images</param>
        /// <returns>boolean representing success of creation</returns>
        Task<bool> CreatePdf(PdfParsedNamedQuery parsedNamedQuery, List<Asset> images);
    }
}