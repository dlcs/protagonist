using DLCS.Core.Types;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using Engine.Data;

namespace Engine.Ingest;

public interface IAssetIngester
{
    /// <summary>
    /// Run ingest based on <see cref="IngestAssetRequest"/>.
    /// </summary>
    /// <returns>Result of ingest operations</returns>
    Task<IngestResult> Ingest(IngestAssetRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains operations for ingesting assets.
/// </summary>
public class AssetIngester(
    ICustomerOriginStrategyRepository customerOriginRepository,
    ILogger<AssetIngester> logger,
    IngestExecutor executor,
    IEngineAssetRepository engineAssetRepository)
    : IAssetIngester
{
    /// <summary>
    /// Run ingest based on <see cref="IngestAssetRequest"/>.
    /// </summary>
    /// <returns>Result of ingest operations</returns>
    public async Task<IngestResult> Ingest(IngestAssetRequest request, CancellationToken cancellationToken = default)
    {
        var asset = await engineAssetRepository.GetAsset(request.Id, request.BatchId, cancellationToken);

        if (asset == null)
        {
            logger.LogError("Could not find an asset for asset id {AssetId}", request.Id);
            return new IngestResult(null, IngestResultStatus.Failed);
        }
        
        // get any matching CustomerOriginStrategy 
        var customerOriginStrategy = await customerOriginRepository.GetCustomerOriginStrategy(asset, true);

        // now ingest the asset
        var status = await executor.IngestAsset(asset, customerOriginStrategy, cancellationToken);
        return status;
    }
}

public class IngestResult(AssetId? assetId, IngestResultStatus ingestResult)
{
    public AssetId? AssetId { get; } = assetId;
    public IngestResultStatus Status { get; } = ingestResult;
}

public class AdjunctIngestResult(string adjunctId, AssetId? assetId, IngestResultStatus ingestResult)
    : IngestResult(assetId, ingestResult)
{
    public string AdjunctId  { get; } = adjunctId;
}
