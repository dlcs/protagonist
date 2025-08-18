using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Customers;
using DLCS.Web.Auth;
using DLCS.Web.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.API;

/// <summary>
/// Implementation of IDlcsApiClient
/// </summary>
public class ApiClient : IDlcsApiClient
{
    private readonly HttpClient httpClient;
    private readonly DlcsApiAuth dlcsApiAuth;
    private readonly ICustomerRepository customerRepository;
    private readonly OrchestratorSettings orchestratorSettings;
    private readonly ILogger<ApiClient> logger;

    public ApiClient(
        HttpClient httpClient, 
        DlcsApiAuth dlcsApiAuth,
        ICustomerRepository customerRepository,
        IOptions<OrchestratorSettings> orchestratorSettings,
        ILogger<ApiClient> logger)
    {
        this.httpClient = httpClient;
        this.dlcsApiAuth = dlcsApiAuth;
        this.customerRepository = customerRepository;
        this.orchestratorSettings = orchestratorSettings.Value;
        this.logger = logger;
    }

    public async Task<bool> ReingestAsset(AssetId assetId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/customers/{assetId.Customer}/spaces/{assetId.Space}/images/{assetId.Asset}/reingest";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            if (!await SetApiAuth(assetId.Customer, request)) return false;
            
            var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            JObject jObject = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (jObject.TryGetValue("error", StringComparison.InvariantCultureIgnoreCase, out var errorToken))
            {
                var errorValue = errorToken.Value<string>();
                if (!string.IsNullOrEmpty(errorValue))
                {
                    logger.LogError("Reingest message processed but error returned for '{AssetId}':{Error}",
                        assetId, errorValue);
                    return false;
                }
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Http exception reingesting asset {AssetId}", assetId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unknown exception reingesting asset {AssetId}", assetId);
        }

        return false;
    }

    private async Task<bool> SetApiAuth(int customerId, HttpRequestMessage request)
    {
        var customer = await customerRepository.GetCustomer(customerId);
        if (customer == null)
        {
            logger.LogWarning("Unable to find customer details for setting API Auth - {CustomerId}", customerId);
            return false;
        }

        var basicAuth = dlcsApiAuth.GetBasicAuthForCustomer(customer, orchestratorSettings.ApiSalt);
        if (string.IsNullOrEmpty(basicAuth))
        {
            logger.LogWarning("Unable to find customer key for API Auth - {CustomerId}", customerId);
            return false;
        }
        request.Headers.AddBasicAuth(basicAuth);
        return true;
    }
}