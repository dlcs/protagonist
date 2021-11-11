using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries.Parsing
{
    /// <summary>
    /// Named query parser for converting objects to Zip archive
    /// </summary>
    public class ZipNamedQueryParser : StoredNamedQueryParser<StoredParsedNamedQuery>
    {
        public ZipNamedQueryParser(IOptions<NamedQuerySettings> namedQuerySettings, ILogger logger) 
            : base(namedQuerySettings, logger)
        {
        }

        protected override StoredParsedNamedQuery GenerateParsedQueryObject(CustomerPathElement customerPathElement)
            => new(customerPathElement);

        protected override string GetTemplateFromSettings(NamedQuerySettings namedQuerySettings)
            => namedQuerySettings.ZipStorageTemplate;
    }
}