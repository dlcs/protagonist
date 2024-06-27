using System.Collections.Generic;
using DLCS.Core.Strings;
using DLCS.Model.Assets.NamedQueries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.NamedQueries.Parsing;

/// <summary>
/// Named query parser for converting objects to PDF
/// </summary>
public class PdfNamedQueryParser : StoredNamedQueryParser<PdfParsedNamedQuery>
{
    // PDF Specific
    private const string CoverPage = "coverpage";
    private const string RedactedMessage = "redactedmessage";

    public PdfNamedQueryParser(IOptions<NamedQueryTemplateSettings> namedQuerySettings, ILogger<PdfNamedQueryParser> logger)
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

    protected override string GetTemplateFromSettings(NamedQueryTemplateSettings namedQuerySettings)
        => namedQuerySettings.PdfStorageTemplate;

    protected override PdfParsedNamedQuery GenerateParsedQueryObject(int customerId)
        => new(customerId);

    protected override void PostParsingOperations(PdfParsedNamedQuery parsedNamedQuery)
    {
        base.PostParsingOperations(parsedNamedQuery);

        if (parsedNamedQuery.CoverPageFormat.HasText())
        {
            parsedNamedQuery.CoverPageUrl = FormatTemplate(parsedNamedQuery.CoverPageFormat, parsedNamedQuery);
        }
    }
}