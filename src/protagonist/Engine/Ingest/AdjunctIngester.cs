using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using Engine.Data;

namespace Engine.Ingest;

public interface IAdjunctIngester
{
    Task<IngestResult> Ingest(IngestAdjunctRequest request, CancellationToken cancellationToken = default);
}

public class AdjunctIngester(
    ICustomerOriginStrategyRepository customerOriginRepository,
    IngestExecutor executor,
    IEngineAssetRepository engineAssetRepository,
    ILogger<AdjunctIngester> logger) : IAdjunctIngester
{
    public async Task<IngestResult> Ingest(IngestAdjunctRequest request, CancellationToken cancellationToken = default)
    {
        var asset = await engineAssetRepository.GetAsset(request.AssetId, null, cancellationToken);

        if (asset == null)
        {
            logger.LogError("Could not find an asset for asset id {AssetId}", request.AssetId);
            return new IngestResult(null, IngestResultStatus.Failed);
        }

        var adjunct = await engineAssetRepository.GetAdjunct(request.Id, request.AssetId, cancellationToken);

        if (adjunct == null)
        {
            logger.LogError("Could not find an adjunct for adjunct id {AdjunctId}, asset id {AssetId}", request.Id,
                request.AssetId);
            return new IngestResult(null, IngestResultStatus.Failed);
        }

        // get any matching CustomerOriginStrategy 
        var customerOriginStrategy =
            await customerOriginRepository.GetCustomerOriginStrategy(adjunct, asset.Customer);

        // now ingest the adjunct
        var status = await executor.IngestAdjunct(asset, adjunct, customerOriginStrategy, cancellationToken);
        return status;
    }
}
