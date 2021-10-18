using System.Collections.Generic;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure.NamedQueries.Parsing
{
    /// <summary>
    /// Named query parser for rendering objects to IIIF
    /// </summary>
    public class IIIFNamedQueryParser : BaseNamedQueryParser<IIIFParsedNamedQuery>
    {
        // IIIF specific
        private const string Manifest = "manifest";

        public IIIFNamedQueryParser(ILogger<IIIFNamedQueryParser> logger)
            : base(logger)
        {
        }

        protected override void CustomHandling(List<string> queryArgs, string key, string value,
            IIIFParsedNamedQuery assetQuery)
        {
            switch (key)
            {
                case Manifest:
                    assetQuery.Manifest = GetQueryMappingFromTemplateElement(value);
                    break;
            }
        }

        protected override IIIFParsedNamedQuery GenerateParsedQueryObject(CustomerPathElement customerPathElement)
            => new(customerPathElement);
    }
}