using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using DLCS.Model.Assets.NamedQueries;

namespace Orchestrator.Features.NamedQueries
{
    /// <summary>
    /// Interface for parsing NamedQueries to generate <see cref="ResourceMappedAssetQuery"/>
    /// </summary>
    public interface INamedQueryParser
    {
        /// <summary>
        /// Generate query from specified Args and NamedQuery record
        /// </summary>
        /// <param name="customer">Customer to run query against.</param>
        /// <param name="namedQueryArgs">Additional args for generating query object.</param>
        /// <param name="namedQueryTemplate">String representing NQ template</param>
        /// <returns><see cref="ResourceMappedAssetQuery"/> object</returns>
        ResourceMappedAssetQuery GenerateResourceMappedAssetQueryFromRequest(int customer, string? namedQueryArgs,
            string namedQueryTemplate);
    }

    /// <summary>
    /// Basic NQ parser, supporting the following arguments: s1, s2, s3, n1, n2, n3, space, spacename, manifest,
    /// sequence, canvas, # and p*
    /// </summary>
    public class BasicNamedQueryParser : INamedQueryParser
    {
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

        public ResourceMappedAssetQuery GenerateResourceMappedAssetQueryFromRequest(int customer, string? namedQueryArgs,
            string namedQueryTemplate)
        {
            namedQueryTemplate.ThrowIfNullOrWhiteSpace(nameof(namedQueryTemplate));
            
            // Split the NQ template into the component parts
            var templatePairing = namedQueryTemplate.Split("&", StringSplitOptions.RemoveEmptyEntries);

            // Get query args - those passed in URL + those appended via #=1 notation in template
            var queryArgs = GetQueryArgsList(namedQueryArgs, templatePairing);

            // Populate the ResourceMappedAssetQuery object using template + query args
            var assetQuery = GenerateResourceMappedAssetQuery(customer, templatePairing, queryArgs);
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
        
        private ResourceMappedAssetQuery GenerateResourceMappedAssetQuery(int customer, string[] templatePairing,
            List<string> queryArgs)
        {
            var assetQuery = new ResourceMappedAssetQuery(customer);

            // Iterate through all of the pairs and generate the NQ model
            foreach (var pair in templatePairing)
            {
                var elements = pair.Split("=", StringSplitOptions.RemoveEmptyEntries);
                if (elements.Length != 2) continue;

                assetQuery.Pairs.Add(elements[0], elements[1]);
                switch (elements[0])
                {
                    case Space:
                        assetQuery.Space = Convert.ToInt32(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                    case SpaceName:
                        assetQuery.SpaceName = GetQueryArgumentFromTemplateElement(queryArgs, elements[1]);
                        assetQuery.ArgumentOrder.Add(pair);
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
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                    case String2:
                        assetQuery.String2 = GetQueryArgumentFromTemplateElement(queryArgs, elements[1]);
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                    case String3:
                        assetQuery.String3 = GetQueryArgumentFromTemplateElement(queryArgs, elements[1]);
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                    case Number1:
                        assetQuery.Number1 = Convert.ToInt64(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                    case Number2:
                        assetQuery.Number2 = Convert.ToInt64(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                    case Number3:
                        assetQuery.Number3 = Convert.ToInt64(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                }
            }

            return assetQuery;
        }
        
        private ResourceMappedAssetQuery.QueryMapping GetQueryMappingFromTemplateElement(string element)
            => element switch
            {
                String1 => ResourceMappedAssetQuery.QueryMapping.String1,
                String2 => ResourceMappedAssetQuery.QueryMapping.String2,
                String3 => ResourceMappedAssetQuery.QueryMapping.String3,
                Number1 => ResourceMappedAssetQuery.QueryMapping.Number1,
                Number2 => ResourceMappedAssetQuery.QueryMapping.Number2,
                Number3 => ResourceMappedAssetQuery.QueryMapping.Number3,
                _ => ResourceMappedAssetQuery.QueryMapping.Unset
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
                
                // TODO - this is thrown but ignored in Deliverator
                throw new ArgumentOutOfRangeException(element, "Not enough query arguments to satisfy template element parameter");
            }
            
            // TODO - this is thrown but ignored in Deliverator
            throw new ArgumentException($"Could not parse template element parameter '{element}'");
        }
    }
}