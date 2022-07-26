using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries.Zip;

/// <summary>
/// Named query parser for converting objects to Zip archive
/// </summary>
public class ZipNamedQueryParser : StoredNamedQueryParser<ZipParsedNamedQuery>
{
    public ZipNamedQueryParser(IOptions<NamedQuerySettings> namedQuerySettings, ILogger<ZipNamedQueryParser> logger)
        : base(namedQuerySettings, logger)
    {
    }

    protected override ZipParsedNamedQuery GenerateParsedQueryObject(CustomerPathElement customerPathElement)
        => new(customerPathElement);

    protected override string GetTemplateFromSettings(NamedQuerySettings namedQuerySettings)
        => namedQuerySettings.ZipStorageTemplate;
}