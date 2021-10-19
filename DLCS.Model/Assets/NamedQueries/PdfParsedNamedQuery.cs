using System.Collections.Generic;
using DLCS.Model.PathElements;

namespace DLCS.Model.Assets.NamedQueries
{
    public class PdfParsedNamedQuery : ParsedNamedQuery
    {
        public PdfParsedNamedQuery(CustomerPathElement customerPathElement) : base(customerPathElement)
        {
        }

        /// <summary>
        /// The format for the output object saved to storage
        /// </summary>
        public string? ObjectNameFormat { get; set; }

        /// <summary>
        /// The parsed and formatted output object to save to storage
        /// </summary>
        public string ObjectName { get; set; } = "Untitled";
        
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

        /// <summary>
        /// A list of all args provided to NQ
        /// </summary>
        public List<string> Args { get; set; } = new();
    }
}