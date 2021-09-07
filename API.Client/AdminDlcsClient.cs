using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using API.Client.JsonLd;
using DLCS.Web.Response;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace API.Client
{
    public class AdminDlcsClient
    {
        private readonly ILogger<AdminDlcsClient> logger;
        private readonly HttpClient httpClient;
        private readonly JsonSerializerSettings jsonSerializerSettings;
        
        public AdminDlcsClient(
            ILogger<AdminDlcsClient> logger,
            HttpClient httpClient)
        {
            this.logger = logger;
            this.httpClient = httpClient;
            jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        public void SetBasicAuth(string basicAuth)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        }
        
        public async Task<Customer> CreateCustomer(Customer customer)
        {
            var url = $"/customers";
            var response = await httpClient.PostAsync(url, ApiBody(customer));
            var newCustomer = await response.ReadAsJsonAsync<Customer>(true, jsonSerializerSettings);
            return newCustomer;
        }
        
        public async Task<PortalUser> CreatePortalUser(PortalUser portalUser, string customerId)
        {
            var url = $"/customers/{customerId}/portalUsers";
            var response = await httpClient.PostAsync(url, ApiBody(portalUser));
            var newUser = await response.ReadAsJsonAsync<PortalUser>(true, jsonSerializerSettings);
            return newUser;
        }
        
        private HttpContent ApiBody(JsonLdBase apiObject)
        {
            var jsonString = JsonConvert.SerializeObject(apiObject, jsonSerializerSettings);
            return new StringContent(jsonString, Encoding.UTF8, "application/json");
        }
        
    }
}