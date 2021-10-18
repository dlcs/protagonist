using System;
using System.Collections.Generic;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure.NamedQueries.Parsing
{
    public class PdfNamedQueryParser : BaseNamedQueryParser<PdfParsedNamedQuery>
    {
        // PDF Specific
        private const string ObjectName = "objectname";
        private const string CoverPage = "coverpage";
        private const string RedactedMessage = "redactedmessage";
        private const string RolesWhitelist = "roles";

        public PdfNamedQueryParser(ILogger<PdfNamedQueryParser> logger) : base(logger)
        {
        }

        protected override void CustomHandling(List<string> queryArgs, string key, string value, PdfParsedNamedQuery assetQuery)
        {
            throw new NotImplementedException();
        }

        protected override PdfParsedNamedQuery GenerateParsedQueryObject(CustomerPathElement customerPathElement)
            => new(customerPathElement);
    }
}