using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using DLCS.Model.Assets.NamedQueries;
using Microsoft.Extensions.Logging;
using QueryMapping = DLCS.Model.Assets.NamedQueries.ParsedNamedQuery.QueryMapping;
using OrderDirection = DLCS.Model.Assets.NamedQueries.ParsedNamedQuery.OrderDirection;
using QueryOrder = DLCS.Model.Assets.NamedQueries.ParsedNamedQuery.QueryOrder;

namespace DLCS.Repository.NamedQueries.Parsing;

/// <summary>
/// Basic NQ parser, supporting the following arguments: s1, s2, s3, n1, n2, n3, space, spacename, canvas, # and p*
/// </summary>
public abstract class BaseNamedQueryParser<T> : INamedQueryParser
    where T : ParsedNamedQuery
{
    protected readonly ILogger Logger;
    
    [Obsolete("Canvas is used for backwards compatibility with Deliverator. Favour assetOrder")]
    protected const string Canvas = "canvas";

    // Common/source
    protected const string AdditionalArgMarker = "#";
    protected const string Number1 = "n1";
    protected const string Number2 = "n2";
    protected const string Number3 = "n3";
    protected const string ParameterPrefix = "p";
    protected const string Space = "space";
    protected const string SpaceName = "spacename";
    protected const string String1 = "s1";
    protected const string String2 = "s2";
    protected const string String3 = "s3";
    protected const string AssetOrdering = "assetOrder";
    protected const string PathReplacement = "%2F";

    public BaseNamedQueryParser(ILogger logger)
    {
        Logger = logger;
    }

    public T GenerateParsedNamedQueryFromRequest<T>(
        int customerId,
        string? namedQueryArgs,
        string namedQueryTemplate,
        string namedQueryName) where T : ParsedNamedQuery
    {
        namedQueryTemplate.ThrowIfNullOrWhiteSpace(nameof(namedQueryTemplate));

        // Split the NQ template into the component parts
        var templatePairing = namedQueryTemplate.Split("&", StringSplitOptions.RemoveEmptyEntries);

        // Get query args - those passed in URL + those appended via #=1 notation in template
        var queryArgs = GetQueryArgsList(namedQueryArgs, templatePairing);

        // Populate the ParsedNamedQuery object using template + query args
        var assetQuery = GenerateParsedNamedQuery(customerId, templatePairing, queryArgs);
        assetQuery.NamedQueryName = namedQueryName;
        PostParsingOperations(assetQuery);
        return (assetQuery as T)!;
    }

    /// <summary>
    /// Optional method to call on parsed query after standard parsing has happened.
    /// </summary>
    /// <param name="parsedNamedQuery"></param>
    protected virtual void PostParsingOperations(T parsedNamedQuery)
    {
    }

    private static List<string> GetQueryArgsList(string? namedQueryArgs, string[] templatePairing)
    {
        // Get any arguments passed to the NQ from URL
        var queryArgs = namedQueryArgs.IsNullOrEmpty()
            ? new List<string>()
            : namedQueryArgs!.Split("/", StringSplitOptions.RemoveEmptyEntries).ToList();

        // Iterate through any pairs that start with '#', as these are treated as additional args
        foreach (var additionalArg in templatePairing.Where(p => p.StartsWith(AdditionalArgMarker)))
        {
            var elements = additionalArg.Split("=", StringSplitOptions.RemoveEmptyEntries);
            if (elements.Length != 2) continue;
            queryArgs.Add(elements[1]);
        }

        return queryArgs;
    }

    private T GenerateParsedNamedQuery(int customerId, string[] templatePairing, List<string> queryArgs)
    {
        var assetQuery = GenerateParsedQueryObject(customerId);

        // Iterate through all of the pairs and generate the NQ model
        try
        {
            foreach (var pair in templatePairing)
            {
                var elements = pair.Split("=", StringSplitOptions.RemoveEmptyEntries);
                if (elements.Length != 2) continue;

                switch (elements[0])
                {
                    case Canvas:
                    case AssetOrdering:
                        assetQuery.AssetOrdering = GetAssetOrderingFromTemplateElement(elements[1]);
                        break;
                    case Space:
                        assetQuery.Space = int.Parse(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                        break;
                    case SpaceName:
                        assetQuery.SpaceName = GetQueryArgumentFromTemplateElement(queryArgs, elements[1]);
                        break;
                    case String1:
                        assetQuery.String1 = GetQueryArgumentFromTemplateElement(queryArgs, elements[1]);
                        break;
                    case String2:
                        assetQuery.String2 = GetQueryArgumentFromTemplateElement(queryArgs, elements[1]);
                        break;
                    case String3:
                        assetQuery.String3 = GetQueryArgumentFromTemplateElement(queryArgs, elements[1]);
                        break;
                    case Number1:
                        assetQuery.Number1 =
                            long.Parse(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                        break;
                    case Number2:
                        assetQuery.Number2 =
                            long.Parse(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                        break;
                    case Number3:
                        assetQuery.Number3 =
                            long.Parse(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                        break;
                }

                CustomHandling(queryArgs, elements[0], elements[1], assetQuery);
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error parsing named query for customer {Customer}", customerId);
            assetQuery.SetError(e.Message);
        }

        return assetQuery;
    }

    /// <summary>
    /// Adds handling for any custom key/value pairs, in addition to the core s1, s2, p1 etc
    /// </summary>
    /// <param name="queryArgs">Collection of query args parsed from request and template</param>
    /// <param name="key">Key of templatePair </param>
    /// <param name="value"></param>
    /// <param name="assetQuery"></param>
    protected abstract void CustomHandling(List<string> queryArgs, string key, string value, T assetQuery);

    /// <summary>
    /// Factory method to generate instance of <see cref="ParsedNamedQuery"/>.
    /// </summary>
    /// <remarks>Could use Activator.CreateInstance this avoids using reflection</remarks>
    protected abstract T GenerateParsedQueryObject(int customerId);

    protected string GetQueryArgumentFromTemplateElement(List<string> args, string element)
    {
        // Arg will be in format p1, p2, p3 etc. Get the index, then extract that element from args list
        if (!element.StartsWith(ParameterPrefix) || element.Length <= 1)
        {
            // default to just return the element as a literal
            return element;
        }

        if (int.TryParse(element[1..], out int argNumber))
        {
            if (args.Count >= argNumber)
            {
                return args[argNumber - 1].Replace(PathReplacement, "/");
            }

            throw new ArgumentOutOfRangeException(element,
                "Not enough query arguments to satisfy template element parameter");
        }

        throw new ArgumentException($"Could not parse template element parameter '{element}'", element);
    }

    /// <summary>
    /// Convert passed string element (e.g. s1, s2) to <see cref="QueryMapping"/>
    /// </summary>
    protected QueryMapping GetQueryMappingFromTemplateElement(string element)
        => element switch
        {
            String1 => QueryMapping.String1,
            String2 => QueryMapping.String2,
            String3 => QueryMapping.String3,
            Number1 => QueryMapping.Number1,
            Number2 => QueryMapping.Number2,
            Number3 => QueryMapping.Number3,
            _ => QueryMapping.Unset
        };

    /// <summary>
    /// Convert ordering element to <see cref="QueryOrder"/>. This can be field(s) with optional direction element
    /// e.g.
    ///   n1
    ///   n2 desc
    ///   n3+asc
    ///   n1;n2 desc;s3+asc
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    protected List<QueryOrder> GetAssetOrderingFromTemplateElement(string element)
    {
        const string fieldDirectionDelimiter = " ";
        var orderings = element.Split(";");
        var orderBy = new List<QueryOrder>(orderings.Length);

        void UpdateOrderByCollection(string field, string direction = "asc")
        {
            var dir = direction.Equals("desc", StringComparison.OrdinalIgnoreCase)
                ? OrderDirection.Descending
                : OrderDirection.Ascending;
            
            var queryMapping = GetQueryMappingFromTemplateElement(field);
            orderBy.Add(new QueryOrder(queryMapping, dir));
        }

        foreach (var ordering in orderings)
        {
            var fieldDirection = ordering
                .Replace("+", fieldDirectionDelimiter)
                .Split(fieldDirectionDelimiter, StringSplitOptions.RemoveEmptyEntries);
            
            if (fieldDirection.Length == 1)
            {
                UpdateOrderByCollection(fieldDirection[0]);
            }
            else
            {
                UpdateOrderByCollection(fieldDirection[0], fieldDirection[1]);
            }
        }

        return orderBy;
    }

    /// <summary>
    /// Replace characters in provided template with values set in parsedNamedQuery.
    /// Supported replacement tokens are {s1}, {s2}, {s3}, {n1}, {n2}, {n3}.
    /// Replacement tokens removed if no matching value provided in NQ. 
    /// </summary>
    protected string? FormatTemplate(string? template, ParsedNamedQuery parsedNamedQuery)
        => template?
            .Replace($"{{{String1}}}", parsedNamedQuery.String1)
            .Replace($"{{{String2}}}", parsedNamedQuery.String2)
            .Replace($"{{{String3}}}", parsedNamedQuery.String3)
            .Replace($"{{{Number1}}}",
                parsedNamedQuery.Number1.HasValue ? parsedNamedQuery.Number1.ToString() : string.Empty)
            .Replace($"{{{Number2}}}",
                parsedNamedQuery.Number2.HasValue ? parsedNamedQuery.Number2.ToString() : string.Empty)
            .Replace($"{{{Number3}}}",
                parsedNamedQuery.Number3.HasValue ? parsedNamedQuery.Number3.ToString() : string.Empty);
}