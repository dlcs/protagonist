using System.Collections.Generic;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure.NamedQueries.Parsing
{
    /// <summary>
    /// Named query parser for converting objects to PDF
    /// </summary>
    public class PdfNamedQueryParser : BaseNamedQueryParser<PdfParsedNamedQuery>
    {
        // PDF Specific
        private const string ObjectName = "objectname";
        private const string CoverPage = "coverpage";
        private const string RedactedMessage = "redactedmessage";

        public PdfNamedQueryParser(ILogger<PdfNamedQueryParser> logger) : base(logger)
        {
        }

        protected override void CustomHandling(List<string> queryArgs, string key, string value,
            PdfParsedNamedQuery assetQuery)
        {
            if (assetQuery.Args.IsNullOrEmpty()) assetQuery.Args = queryArgs;
            
            switch (key)
            {
                case ObjectName:
                    assetQuery.ObjectNameFormat = GetQueryArgumentFromTemplateElement(queryArgs, value);
                    break;
                case CoverPage:
                    assetQuery.CoverPageFormat = GetQueryArgumentFromTemplateElement(queryArgs, value);
                    break;
                case RedactedMessage:
                    assetQuery.RedactedMessage = GetQueryArgumentFromTemplateElement(queryArgs, value);
                    break;
            }
        }

        protected override PdfParsedNamedQuery GenerateParsedQueryObject(CustomerPathElement customerPathElement)
            => new(customerPathElement);

        protected override void PostParsingOperations(PdfParsedNamedQuery parsedNamedQuery)
        {
            if (parsedNamedQuery.ObjectNameFormat.HasText())
            {
                parsedNamedQuery.ObjectName = FormatTemplate(parsedNamedQuery.ObjectNameFormat, parsedNamedQuery);
            }

            if (parsedNamedQuery.CoverPageFormat.HasText())
            {
                parsedNamedQuery.CoverPageUrl = FormatTemplate(parsedNamedQuery.CoverPageFormat, parsedNamedQuery);
            }
        }
    }
}