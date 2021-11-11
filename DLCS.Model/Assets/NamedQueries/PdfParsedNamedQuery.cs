using DLCS.Model.PathElements;

namespace DLCS.Model.Assets.NamedQueries
{
    public class PdfParsedNamedQuery : StoredParsedNamedQuery
    {
        public PdfParsedNamedQuery(CustomerPathElement customerPathElement) : base(customerPathElement)
        {
        }

        /// <summary>
        /// Format of URL of existing pdf to be used as coverpage for generated pdf
        /// </summary>
        public string? CoverPageFormat { get; set; }
        
        /// <summary>
        /// URL of existing pdf to be used as coverpage for generated pdf
        /// </summary>
        public string? CoverPageUrl { get; set; }

        /// <summary>
        /// Message to show on any pages that have items that require auth.
        /// </summary>
        public string RedactedMessage { get; set; } = "Unable to display page";
    }
}