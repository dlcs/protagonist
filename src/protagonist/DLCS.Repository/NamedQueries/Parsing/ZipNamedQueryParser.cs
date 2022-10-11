using DLCS.Model.Assets.NamedQueries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.NamedQueries.Parsing;

/// <summary>
/// Named query parser for converting objects to Zip archive
/// </summary>
public class ZipNamedQueryParser : StoredNamedQueryParser<ZipParsedNamedQuery>
{
    public ZipNamedQueryParser(IOptions<NamedQueryTemplateSettings> namedQuerySettings, ILogger<ZipNamedQueryParser> logger)
        : base(namedQuerySettings, logger)
    {
    }

    protected override ZipParsedNamedQuery GenerateParsedQueryObject(int customerId)
        => new(customerId);

    protected override string GetTemplateFromSettings(NamedQueryTemplateSettings namedQuerySettings)
        => namedQuerySettings.ZipStorageTemplate;
}