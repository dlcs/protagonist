using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using MediatR;
using Orchestrator.Models;

namespace Orchestrator.Features.NamedQuery.Requests
{
    /// <summary>
    /// Mediatr request for generating manifest using a named query.
    /// </summary>
    public class GetNamedQueryResults : IRequest<DescriptionResourceResponse>
    {
        public string CustomerPathValue { get; }
        
        public string NamedQuery { get; }
        
        public string ExtraArgs { get; }

        public GetNamedQueryResults(string customerPathValue, string namedQuery, string extraArgs)
        {
            CustomerPathValue = customerPathValue;
            NamedQuery = namedQuery;
            ExtraArgs = extraArgs;
        }
    }
    
    public class GetNamedQueryResultsHandler : IRequestHandler<GetNamedQueryResults, DescriptionResourceResponse>
    {
        private readonly IPathCustomerRepository pathCustomerRepository;
        private readonly NamedQueryConductor namedQueryConductor;

        public GetNamedQueryResultsHandler(IPathCustomerRepository pathCustomerRepository, NamedQueryConductor namedQueryConductor)
        {
            this.pathCustomerRepository = pathCustomerRepository;
            this.namedQueryConductor = namedQueryConductor;
        }
        
        public async Task<DescriptionResourceResponse> Handle(GetNamedQueryResults request, CancellationToken cancellationToken)
        {
            var customerPathElement = await pathCustomerRepository.GetCustomer(request.CustomerPathValue);

            var images =
                namedQueryConductor.GetNamedQueryAssetsForRequest(request.NamedQuery, customerPathElement.Id,
                    request.ExtraArgs);

            return DescriptionResourceResponse.Empty;
        }
    }

    public class NamedQueryConductor
    {
        private readonly INamedQueryRepository namedQueryRepository;
        private const string AdditionalArgMarker = "#";

        public NamedQueryConductor(INamedQueryRepository namedQueryRepository)
        {
            this.namedQueryRepository = namedQueryRepository;
        }
        
        public async Task<IEnumerable<Asset>> GetNamedQueryAssetsForRequest(string queryName, int customer, string args)
        {
            var namedQuery = await namedQueryRepository.GetByName(customer, queryName);
            if (namedQuery == null)
            {
                return Enumerable.Empty<Asset>();
            }
            
            // Split the NQ template into the component parts
            var templatePairing = namedQuery.Template.Split("&", StringSplitOptions.RemoveEmptyEntries);
            
            // Get query args - those passed in URL + those appended via #=1 notation in template
            var queryArgs = GetQueryArgsList(args, templatePairing);

            // Populate the ResourceMappedAssetQuery object using template + query args
            var assetQuery = GenerateResourceMappedAssetQuery(customer, templatePairing, queryArgs);

            // Get the images that match NQ results
            var images = await namedQueryRepository.GetNamedQueryResults(assetQuery);
            return images;
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
                    case "space":
                        assetQuery.Space = Convert.ToInt32(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                    case "spacename":
                        assetQuery.SpaceName = GetQueryArgumentFromTemplateElement(queryArgs, elements[1]);
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                    case "manifest":
                        assetQuery.Manifest = GetQueryMappingFromTemplateElement(elements[1]);
                        break;
                    case "sequence":
                        assetQuery.Sequence = GetQueryMappingFromTemplateElement(elements[1]);
                        break;
                    case "canvas":
                        assetQuery.Canvas = GetQueryMappingFromTemplateElement(elements[1]);
                        break;
                    case "s1":
                        assetQuery.String1 = GetQueryArgumentFromTemplateElement(queryArgs, elements[1]);
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                    case "s2":
                        assetQuery.String2 = GetQueryArgumentFromTemplateElement(queryArgs, elements[1]);
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                    case "s3":
                        assetQuery.String3 = GetQueryArgumentFromTemplateElement(queryArgs, elements[1]);
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                    case "n1":
                        assetQuery.Number1 = Convert.ToInt64(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                    case "n2":
                        assetQuery.Number2 = Convert.ToInt64(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                    case "n3":
                        assetQuery.Number3 = Convert.ToInt64(GetQueryArgumentFromTemplateElement(queryArgs, elements[1]));
                        assetQuery.ArgumentOrder.Add(pair);
                        break;
                }
            }

            return assetQuery;
        }

        private static List<string> GetQueryArgsList(string args, string[] templatePairing)
        {
            // Get any arguments passed to the NQ from URL
            var queryArgs = args.Split("/", StringSplitOptions.RemoveEmptyEntries).ToList();

            // Iterate through any pairs that start with '#', as these are treated as additional args
            foreach (var additionalArg in templatePairing.Where(p => p.StartsWith("#")))
            {
                var elements = additionalArg.Split("=", StringSplitOptions.RemoveEmptyEntries);
                if (elements.Length != 2) continue;
                queryArgs.Add(elements[1]);
            }

            return queryArgs;
        }

        private ResourceMappedAssetQuery.QueryMapping GetQueryMappingFromTemplateElement(string element)
            => element switch
            {
                "s1" => ResourceMappedAssetQuery.QueryMapping.String1,
                "s2" => ResourceMappedAssetQuery.QueryMapping.String2,
                "s3" => ResourceMappedAssetQuery.QueryMapping.String3,
                "n1" => ResourceMappedAssetQuery.QueryMapping.Number1,
                "n2" => ResourceMappedAssetQuery.QueryMapping.Number2,
                "n3" => ResourceMappedAssetQuery.QueryMapping.Number3,
                _ => ResourceMappedAssetQuery.QueryMapping.Unset
            };

        private string GetQueryArgumentFromTemplateElement(List<string> args, string element)
        {
            // Arg will be in format p1, p2, p3 etc. Get the index, then extract that element from args list
            if (!element.StartsWith("p") || element.Length <= 1)
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