using System.Collections.Generic;
using DLCS.Core.Strings;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries.PDF
{
    /// <summary>
    /// Named query parser for converting objects to PDF
    /// </summary>
    public class PdfNamedQueryParser : StoredNamedQueryParser<PdfParsedNamedQuery>
    {
        // PDF Specific
        private const string CoverPage = "coverpage";
        private const string RedactedMessage = "redactedmessage";

        public PdfNamedQueryParser(IOptions<NamedQuerySettings> namedQuerySettings, ILogger<PdfNamedQueryParser> logger)
            : base(namedQuerySettings, logger)
        {
        }

        protected override void CustomHandling(List<string> queryArgs, string key, string value,
            PdfParsedNamedQuery assetQuery)
        {
            base.CustomHandling(queryArgs, key, value, assetQuery);
            
            switch (key)
            {
                case CoverPage:
                    assetQuery.CoverPageFormat = GetQueryArgumentFromTemplateElement(queryArgs, value);
                    break;
                case RedactedMessage:
                    assetQuery.RedactedMessage = GetQueryArgumentFromTemplateElement(queryArgs, value);
                    break;
            }
        }

        protected override string GetTemplateFromSettings(NamedQuerySettings namedQuerySettings)
            => namedQuerySettings.PdfStorageTemplate;

        protected override PdfParsedNamedQuery GenerateParsedQueryObject(CustomerPathElement customerPathElement)
            => new(customerPathElement);

        protected override void PostParsingOperations(PdfParsedNamedQuery parsedNamedQuery)
        {
            base.PostParsingOperations(parsedNamedQuery);

            if (parsedNamedQuery.CoverPageFormat.HasText())
            {
                parsedNamedQuery.CoverPageUrl = FormatTemplate(parsedNamedQuery.CoverPageFormat, parsedNamedQuery);
            }
        }
    }
}