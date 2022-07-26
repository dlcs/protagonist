using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.Core.Encryption;
using DLCS.Core.Types;
using DLCS.Model.Customers;
using DLCS.Web.Auth;
using DLCS.Web.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.Deliverator;

/// <summary>
/// Implementation of IDlcsApiClient using Deliverator API
/// </summary>
public class DeliveratorApiClient : IDlcsApiClient
{
    private readonly HttpClient httpClient;
    private readonly DeliveratorApiAuth deliveratorApiAuth;
    private readonly ICustomerRepository customerRepository;
    private readonly OrchestratorSettings orchestratorSettings;
    private readonly ILogger<DeliveratorApiClient> logger;

    public DeliveratorApiClient(
        HttpClient httpClient, 
        DeliveratorApiAuth deliveratorApiAuth,
        ICustomerRepository customerRepository,
        IOptions<OrchestratorSettings> orchestratorSettings,
        ILogger<DeliveratorApiClient> logger)
    {
        this.httpClient = httpClient;
        this.deliveratorApiAuth = deliveratorApiAuth;
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
            
            // TODO - need to post something, but not an empty body
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

        var basicAuth = deliveratorApiAuth.GetBasicAuthForCustomer(customer, orchestratorSettings.ApiSalt);
        if (string.IsNullOrEmpty(basicAuth))
        {
            logger.LogWarning("Unable to find customer key for API Auth - {CustomerId}", customerId);
            return false;
        }
        request.Headers.AddBasicAuth(basicAuth);
        return true;
    }
}