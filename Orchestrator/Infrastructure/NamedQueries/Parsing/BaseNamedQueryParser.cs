using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure.NamedQueries.Parsing
{
    /// <summary>
    /// Basic NQ parser, supporting the following arguments: s1, s2, s3, n1, n2, n3, space, spacename, canvas, # and p*
    /// </summary>
    public abstract class BaseNamedQueryParser<T> : INamedQueryParser
        where T : ParsedNamedQuery
    {
        protected readonly ILogger Logger;

        // Common/source
        protected const string AdditionalArgMarker = "#";
        protected const string Canvas = "canvas";
        protected const string Number1 = "n1";
        protected const string Number2 = "n2";
        protected const string Number3 = "n3";
        protected const string ParameterPrefix = "p";
        protected const string Space = "space";
        protected const string SpaceName = "spacename";
        protected const string String1 = "s1";
        protected const string String2 = "s2";
        protected const string String3 = "s3";

        public BaseNamedQueryParser(ILogger logger)
        {
            Logger = logger;
        }

        public T GenerateParsedNamedQueryFromRequest<T>(
            CustomerPathElement customerPathElement,
            string? namedQueryArgs,
            string namedQueryTemplate) where T : ParsedNamedQuery
        {
            namedQueryTemplate.ThrowIfNullOrWhiteSpace(nameof(namedQueryTemplate));

            // Split the NQ template into the component parts
            var templatePairing = namedQueryTemplate.Split("&", StringSplitOptions.RemoveEmptyEntries);

            // Get query args - those passed in URL + those appended via #=1 notation in template
            var queryArgs = GetQueryArgsList(namedQueryArgs, templatePairing);

            // Populate the ParsedNamedQuery object using template + query args
            var assetQuery = GenerateParsedNamedQuery(customerPathElement, templatePairing, queryArgs);
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

        private T GenerateParsedNamedQuery(CustomerPathElement customer,
            string[] templatePairing,
            List<string> queryArgs)
        {
            var assetQuery = GenerateParsedQueryObject(customer);

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
                            assetQuery.Canvas = GetQueryMappingFromTemplateElement(elements[1]);
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
                Logger.LogError(e, "Error parsing named query for customer {Customer}", customer.Id);
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
        protected abstract T GenerateParsedQueryObject(CustomerPathElement customerPathElement);

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
                    return args[argNumber - 1];
                }

                throw new ArgumentOutOfRangeException(element,
                    "Not enough query arguments to satisfy template element parameter");
            }

            throw new ArgumentException($"Could not parse template element parameter '{element}'", element);
        }

        /// <summary>
        /// Convert passed string element (e.g. s1, s2) to <see cref="ParsedNamedQuery.QueryMapping"/>
        /// </summary>
        protected ParsedNamedQuery.QueryMapping GetQueryMappingFromTemplateElement(string element)
            => element switch
            {
                String1 => ParsedNamedQuery.QueryMapping.String1,
                String2 => ParsedNamedQuery.QueryMapping.String2,
                String3 => ParsedNamedQuery.QueryMapping.String3,
                Number1 => ParsedNamedQuery.QueryMapping.Number1,
                Number2 => ParsedNamedQuery.QueryMapping.Number2,
                Number3 => ParsedNamedQuery.QueryMapping.Number3,
                _ => ParsedNamedQuery.QueryMapping.Unset
            };

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
}