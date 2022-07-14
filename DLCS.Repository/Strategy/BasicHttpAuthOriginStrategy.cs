using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Strategy
{
    /// <summary>
    /// OriginStrategy implementation for 'basic-http-authentication' assets.
    /// </summary>
    public class BasicHttpAuthOriginStrategy : IOriginStrategy
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ICredentialsRepository credentialsRepository;
        private readonly ILogger<BasicHttpAuthOriginStrategy> logger;
        
        public OriginStrategyType Strategy => OriginStrategyType.BasicHttp;
        
        public BasicHttpAuthOriginStrategy(
            IHttpClientFactory httpClientFactory,
            ICredentialsRepository credentialsRepository,
            ILogger<BasicHttpAuthOriginStrategy> logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.credentialsRepository = credentialsRepository;
            this.logger = logger;
        }

        public async Task<OriginResponse?> LoadAssetFromOrigin(AssetId assetId, string origin,
            CustomerOriginStrategy? customerOriginStrategy, CancellationToken cancellationToken = default)
        {
            logger.LogDebug("Fetching {asset} from Origin: {url}", assetId, origin);
            customerOriginStrategy.ThrowIfNull(nameof(customerOriginStrategy));

            try
            {
                var response = await GetHttpResponse(customerOriginStrategy, cancellationToken, origin);
                var originResponse = await CreateOriginResponse(response);
                return originResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching {asset} from Origin: {url}", assetId, origin);
                return OriginResponse.Empty;
            }
        }

        private async Task<HttpResponseMessage> GetHttpResponse(CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken,
            string assetOrigin)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, assetOrigin);
            request.Headers.Authorization = await SetBasicAuthHeader(customerOriginStrategy);

            var httpClient = httpClientFactory.CreateClient(HttpClients.OriginStrategy);
            var response = await httpClient.SendAsync(request, cancellationToken);
            return response;
        }

        private async Task<AuthenticationHeaderValue> SetBasicAuthHeader(CustomerOriginStrategy customerOriginStrategy)
        {
            var basicCredentials =
                await credentialsRepository.GetBasicCredentialsForOriginStrategy(customerOriginStrategy);

            if (basicCredentials == null)
            {
                throw new ApplicationException(
                    $"Could not find credentials for customerOriginStrategy {customerOriginStrategy.Id}");
            }
            
            var creds = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{basicCredentials.User}:{basicCredentials.Password}"));
            return AuthenticationHeaderValue.Parse($"Basic {creds}");
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