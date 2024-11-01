using System.Collections.Generic;
using DLCS.Model.Assets.NamedQueries;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.NamedQueries.Parsing;

/// <summary>
/// Named query parser for rendering raw asset results
/// </summary>
public class RawNamedQueryParser : BaseNamedQueryParser<ParsedNamedQuery>
{
    public RawNamedQueryParser(ILogger<IIIFNamedQueryParser> logger)
        : base(logger)
    {
    }
    
    protected override void CustomHandling(List<string> queryArgs, string key, string value, ParsedNamedQuery assetQuery)
    {
        // no-op
    }

    protected override ParsedNamedQuery GenerateParsedQueryObject(int customerId)
        => new(customerId);
}