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

namespace DLCS.Repository.Strategy;

/// <summary>
/// OriginStrategy implementation for 'basic-http-authentication' assets.
/// </summary>
public class BasicHttpAuthOriginStrategy(
    IHttpClientFactory httpClientFactory,
    ICredentialsRepository credentialsRepository,
    ILogger<BasicHttpAuthOriginStrategy> logger)
    : IOriginStrategy
{
    public async Task<OriginResponse> LoadAssetFromOrigin(AssetId assetId, string origin,
        CustomerOriginStrategy? customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching {Asset} from Origin: {Url}", assetId, origin);
        customerOriginStrategy = customerOriginStrategy.ThrowIfNull(nameof(customerOriginStrategy));

        try
        {
            var response = await GetHttpResponse(customerOriginStrategy, origin, cancellationToken);
            var originResponse = await response.CreateOriginResponse(cancellationToken);
            return originResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching {Asset} from Origin: {Url}", assetId, origin);
            return OriginResponse.Empty;
        }
    }

    private async Task<HttpResponseMessage> GetHttpResponse(CustomerOriginStrategy customerOriginStrategy,
        string assetOrigin, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, assetOrigin);
        request.Headers.Authorization = await SetBasicAuthHeader(customerOriginStrategy);

        var httpClient = httpClientFactory.CreateClient(HttpClients.OriginStrategy);
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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

        var creds = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{basicCredentials.User}:{basicCredentials.Password}"));
        return AuthenticationHeaderValue.Parse($"Basic {creds}");
    }
}
