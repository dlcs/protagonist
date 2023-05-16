using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DLCS.Web.Response;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using DLCS.HydraModel;
using Hydra;

namespace API.Client;

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
    
    public async Task<Customer?> CreateCustomer(Customer customer)
    {
        var url = $"/customers";
        var response = await httpClient.PostAsync(url, ApiBody(customer));
        var newCustomer = await response.ReadAsJsonAsync<Customer>(true, jsonSerializerSettings);
        return newCustomer;
    }
    
    public async Task<PortalUser?> CreatePortalUser(PortalUser portalUser, string customerResource)
    {
        var url = $"{customerResource}/portalUsers";
        var response = await httpClient.PostAsync(url, ApiBody(portalUser));
        var newUser = await response.ReadAsJsonAsync<PortalUser>(true, jsonSerializerSettings);
        return newUser;
    }
    
    public async Task<ApiKey?> CreateNewApiKey(string customerResource)
    {
        var url = $"{customerResource}/keys";
        var response = await httpClient.PostAsync(url, null!);
        var apiKey = await response.ReadAsJsonAsync<ApiKey>();
        return apiKey;
    }
    
    private HttpContent ApiBody(JsonLdBase apiObject)
    {
        var jsonString = JsonConvert.SerializeObject(apiObject, jsonSerializerSettings);
        return new StringContent(jsonString, Encoding.UTF8, "application/json");
    }
    
}