using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Strategy;

/// <summary>
/// OriginStrategy implementation for 'default' assets.
/// </summary>
public class DefaultOriginStrategy(IHttpClientFactory httpClientFactory, ILogger<DefaultOriginStrategy> logger)
    : IOriginStrategy
{
    public async Task<OriginResponse> LoadFromOrigin(IOriginItem originItem,
        CustomerOriginStrategy? customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching {ItemDesc} from Origin: {Url}", originItem.Identifier(), originItem.Origin);

        try
        {
            var httpClient = httpClientFactory.CreateClient(HttpClients.OriginStrategy);
            var response =
                await httpClient.GetAsync(originItem.Origin, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var originResponse = await response.CreateOriginResponse(cancellationToken);
            return originResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching {ItemDesc} from Origin: {Url}", originItem.Identifier(), originItem.Origin);
            return OriginResponse.Empty;
        }
    }
}
