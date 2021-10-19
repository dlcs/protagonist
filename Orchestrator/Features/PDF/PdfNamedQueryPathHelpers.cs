using DLCS.Core.Strings;
using DLCS.Model.Assets.NamedQueries;

namespace Orchestrator.Features.PDF
{
    internal static class PdfNamedQueryPathHelpers
    {
        public static string GetPdfKey(string pdfControlFileTemplate, PdfParsedNamedQuery parsedNamedQuery,
            string queryName, bool isControlFile)
        {
            var key = pdfControlFileTemplate
                .Replace("{customer}", parsedNamedQuery.Customer.ToString())
                .Replace("{queryname}", queryName)
                .Replace("{args}", string.Join("/", parsedNamedQuery.Args));
            
            if (parsedNamedQuery.ObjectName.HasText()) key += parsedNamedQuery.ObjectName;
            if (isControlFile) key += ".json";
            return key;
        }
    }
}