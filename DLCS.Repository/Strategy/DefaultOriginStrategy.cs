using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Customer;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Strategy
{
    /// <summary>
    /// OriginStrategy implementation for 'default' assets.
    /// </summary>
    public class DefaultOriginStrategy : SafetyCheckOriginStrategy
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger<DefaultOriginStrategy> logger;

        public DefaultOriginStrategy(IHttpClientFactory httpClientFactory, ILogger<DefaultOriginStrategy> logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
        }

        public override OriginStrategyType Strategy => OriginStrategyType.Default;

        protected override async Task<OriginResponse?> LoadAssetFromOriginImpl(Asset asset,
            CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
        {
            // NOTE(DG): This will follow up to 8 redirections, as per deliverator.
            // However, https -> http will fail. 
            // Need to test relative redirects too.
            var assetOrigin = asset.GetIngestOrigin();
            logger.LogDebug("Fetching asset from Origin: {url}", assetOrigin);

            try
            {
                var httpClient = httpClientFactory.CreateClient(HttpClients.OriginStrategy);
                var response = await httpClient.GetAsync(assetOrigin, cancellationToken);
                var originResponse = await CreateOriginResponse(response);
                return originResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching asset from Origin: {url}", assetOrigin);
                return null;
            }
        }
        
        private static async Task<OriginResponse> CreateOriginResponse(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            var content = response.Content;
            if (content == null)
            {
                return new OriginResponse(Stream.Null);
            }

            return new OriginResponse(await content.ReadAsStreamAsync())
                .WithContentLength(content.Headers.ContentLength)
                .WithContentType(content.Headers?.ContentType?.MediaType);
        }
    }
}