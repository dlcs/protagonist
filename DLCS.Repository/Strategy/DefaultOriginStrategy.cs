using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Customers;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Strategy
{
    /// <summary>
    /// OriginStrategy implementation for 'default' assets.
    /// </summary>
    public class DefaultOriginStrategy : IOriginStrategy
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger<DefaultOriginStrategy> logger;
        
        public DefaultOriginStrategy(IHttpClientFactory httpClientFactory, ILogger<DefaultOriginStrategy> logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
        }

        public async Task<OriginResponse?> LoadAssetFromOrigin(AssetId assetId, string origin,
            CustomerOriginStrategy? customerOriginStrategy, CancellationToken cancellationToken = default)
        {
            // NOTE(DG): This will follow up to 8 redirections, as per deliverator.
            // However, https -> http will fail. 
            // Need to test relative redirects too.
            logger.LogDebug("Fetching {Asset} from Origin: {Url}", assetId, origin);

            try
            {
                var httpClient = httpClientFactory.CreateClient(HttpClients.OriginStrategy);
                var response = await httpClient.GetAsync(origin, cancellationToken);
                var originResponse = await CreateOriginResponse(response);
                return originResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching {Asset} from Origin: {Url}", assetId, origin);
                return OriginResponse.Empty;
            }
        }
        
        private static async Task<OriginResponse> CreateOriginResponse(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            var content = response.Content;
            if (content == null)
            {
                return OriginResponse.Empty;
            }

            return new OriginResponse(await content.ReadAsStreamAsync())
                .WithContentLength(content.Headers.ContentLength)
                .WithContentType(content.Headers?.ContentType?.MediaType);
        }
    }
}