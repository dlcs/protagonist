using DLCS.Model.PathElements;

namespace DLCS.Model.Assets.NamedQueries
{
    public class PdfParsedNamedQuery : ParsedNamedQuery
    {
        public string ObjectName { get; set; }
        public string CoverPage { get; set; }
        public string RedactedMessage { get; set; }
        
        public PdfParsedNamedQuery(CustomerPathElement customerPathElement) : base(customerPathElement)
        {
        }
    }
}