using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using API.JsonLd;
using DLCS.Web.Response;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Portal.Legacy
{
    /// <summary>
    /// Client for Dlcs API
    /// </summary>
    public class DlcsClient
    {
        private readonly ILogger<DlcsClient> logger;
        private readonly HttpClient httpClient;
        private readonly ClaimsPrincipal currentUser;

        public DlcsClient(
            ILogger<DlcsClient> logger,
            HttpClient httpClient,
            ClaimsPrincipal currentUser)
        {
            this.logger = logger;
            this.httpClient = httpClient;
            this.currentUser = currentUser;

            var basicAuth = currentUser.GetApiCredentials();
            if (!string.IsNullOrEmpty(basicAuth))
            {
                this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
            }
        }

        public async Task<JObject> GetSpaceDetails(int spaceId)
        {
            // TODO - this is a sample method call to verify API call
            var url = $"/customers/{currentUser.GetCustomerId()}/spaces/{spaceId}";
            var result = await httpClient.GetStringAsync(url);
            return JObject.Parse(result);
        }

        public async Task<IEnumerable<string>?> GetApiKeys()
        {
            var url = $"/customers/{currentUser.GetCustomerId()}/keys";
            var response = await httpClient.GetAsync(url);
            var apiKeys = await response.ReadAsJsonAsync<Collection<ApiKey>>();
            return apiKeys?.Member.Select(m => m.Key);
        }

        public async Task<ApiKey> CreateNewApiKey()
        {
            var url = $"/customers/{currentUser.GetCustomerId()}/keys";
            var response = await httpClient.PostAsync(url, null!);
            var apiKey = await response.ReadAsJsonAsync<ApiKey>();
            return apiKey;
        }
    }
}