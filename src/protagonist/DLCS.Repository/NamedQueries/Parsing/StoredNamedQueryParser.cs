using System.Collections.Generic;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Assets.NamedQueries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.NamedQueries.Parsing;

/// <summary>
/// Base named query parser for rendering objects that are stored and managed via a control-file.
/// </summary>
public abstract class StoredNamedQueryParser<T> : BaseNamedQueryParser<T>
    where T : StoredParsedNamedQuery
{
    private readonly NamedQueryTemplateSettings namedQuerySettings;

    private const string ObjectName = "objectname";

    protected StoredNamedQueryParser(IOptions<NamedQueryTemplateSettings> namedQuerySettings, ILogger logger) : base(logger)
    {
        this.namedQuerySettings = namedQuerySettings.Value;
    }

    protected override void CustomHandling(List<string> queryArgs, string key, string value,
        T assetQuery)
    {
        if (assetQuery.Args.IsNullOrEmpty()) assetQuery.Args = queryArgs;

        switch (key)
        {
            case ObjectName:
                assetQuery.ObjectNameFormat = GetQueryArgumentFromTemplateElement(queryArgs, key, value);
                break;
        }
    }

    /// <summary>
    /// Get the template to use from specified <see cref="NamedQuerySettings"/> object.
    /// </summary>
    /// <returns>Template to use containing {customer}, {queryname} + {args} replacements.</returns>
    protected abstract string GetTemplateFromSettings(NamedQueryTemplateSettings namedQuerySettings);

    protected override void PostParsingOperations(T parsedNamedQuery)
    {
        if (parsedNamedQuery.ObjectNameFormat.HasText())
        {
            parsedNamedQuery.ObjectName = FormatTemplate(parsedNamedQuery.ObjectNameFormat, parsedNamedQuery);
        }

        parsedNamedQuery.StorageKey = GetStorageKey(parsedNamedQuery, false);
        parsedNamedQuery.ControlFileStorageKey = GetStorageKey(parsedNamedQuery, true);
    }

    protected virtual string GetStorageKey(T parsedNamedQuery, bool isControlFile)
    {
        var key = GetTemplateFromSettings(namedQuerySettings)
            .Replace("{customer}", parsedNamedQuery.Customer.ToString())
            .Replace("{queryname}", parsedNamedQuery.NamedQueryName)
            .Replace("{args}", string.Join("/", parsedNamedQuery.Args));

        if (parsedNamedQuery.ObjectName.HasText()) key += $"/{parsedNamedQuery.ObjectName}";
        if (isControlFile) key += ".json";
        return key;
    }
}