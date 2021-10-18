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
        private const string Element = "canvas";
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
                case Element:
                    assetQuery.Canvas = GetQueryMappingFromTemplateElement(value);
                    break;
            }
        }

        protected override IIIFParsedNamedQuery GenerateParsedQueryObject(CustomerPathElement customerPathElement)
            => new(customerPathElement);
        
        private IIIFParsedNamedQuery.QueryMapping GetQueryMappingFromTemplateElement(string element)
            => element switch
            {
                String1 => IIIFParsedNamedQuery.QueryMapping.String1,
                String2 => IIIFParsedNamedQuery.QueryMapping.String2,
                String3 => IIIFParsedNamedQuery.QueryMapping.String3,
                Number1 => IIIFParsedNamedQuery.QueryMapping.Number1,
                Number2 => IIIFParsedNamedQuery.QueryMapping.Number2,
                Number3 => IIIFParsedNamedQuery.QueryMapping.Number3,
                _ => IIIFParsedNamedQuery.QueryMapping.Unset
            };
    }
}