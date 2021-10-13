using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure.NamedQueries
{
    /// <summary>
    /// Basic NQ parser, supporting the following arguments: s1, s2, s3, n1, n2, n3, space, spacename, manifest,
    /// sequence, canvas, # and p*
    /// </summary>
    public class BasicNamedQueryParser : INamedQueryParser
    {
        private readonly ILogger<BasicNamedQueryParser> logger;
        private const string AdditionalArgMarker = "#";
        private const string Element = "canvas";
        private const string Manifest = "manifest";
        private const string Number1 = "n1";
        private const string Number2 = "n2";
        private const string Number3 = "n3";
        private const string ParameterPrefix = "p";
        private const string Sequence = "sequence";
        private const string Space = "space";
        private const string SpaceName = "spacename";
        private const string String1 = "s1";
        private const string String2 = "s2";
        private const string String3 = "s3";

        public BasicNamedQueryParser(ILogger<BasicNamedQueryParser> logger)
        {
            this.logger = logger;
        }

        public ParsedNamedQuery GenerateParsedNamedQueryFromRequest(
            CustomerPathElement customerPathElement, 
            string? namedQueryArgs,
            string namedQueryTemplate)
        {
            namedQueryTemplate.ThrowIfNullOrWhiteSpace(nameof(namedQueryTemplate));
            
            // Split the NQ template into the component parts
            var templatePairing = namedQueryTemplate.Split("&", StringSplitOptions.RemoveEmptyEntries);

            // Get query args - those passed in URL + those appended via #=1 notation in template
            var queryArgs = GetQueryArgsList(namedQueryArgs, templatePairing);

            // Populate the ParsedNamedQuery object using template + query args
            var assetQuery = GenerateParsedNamedQuery(customerPathElement, templatePairing, queryArgs);
            return assetQuery;
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
        
        private ParsedNamedQuery GenerateParsedNamedQuery(CustomerPathElement customer, 
            string[] templatePairing,
            List<string> queryArgs)
        {
            var assetQuery = new ParsedNamedQuery(customer);

            // Iterate through all of the pairs and generate the NQ model
            try
            {
                foreach (var pair in templatePairing)
                {
                    var elements = pair.Split("=", StringSplitOptions.RemoveEmptyEntries);
                    if (elements.Length != 2) continue;

                    switch (elements[0])
                    {
                        case Space:
                            assetQuery.Space = int.Parse(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                            break;
                        case SpaceName:
                            assetQuery.SpaceName = GetQueryArgumentFromTemplateElement(queryArgs, elements[1]);
                            break;
                        case Manifest:
                            assetQuery.Manifest = GetQueryMappingFromTemplateElement(elements[1]);
                            break;
                        case Sequence:
                            assetQuery.Sequence = GetQueryMappingFromTemplateElement(elements[1]);
                            break;
                        case Element:
                            assetQuery.Canvas = GetQueryMappingFromTemplateElement(elements[1]);
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
                            assetQuery.Number1 = long.Parse(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                            break;
                        case Number2:
                            assetQuery.Number2 = long.Parse(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                            break;
                        case Number3:
                            assetQuery.Number3 = long.Parse(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error parsing named query for customer {Customer}", customer.Id);
                assetQuery.SetError(e.Message);
            }

            return assetQuery;
        }
        
        private ParsedNamedQuery.QueryMapping GetQueryMappingFromTemplateElement(string element)
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

        private string GetQueryArgumentFromTemplateElement(List<string> args, string element)
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
    }
}