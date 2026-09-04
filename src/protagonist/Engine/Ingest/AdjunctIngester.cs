using DLCS.AWS.Configuration;
using DLCS.Core.Types;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using Engine.Data;
using Microsoft.AspNetCore.Components;

namespace Engine.Ingest;

public interface IAdjunctIngester
{
    /// <summary>
    /// Run ingest based on <see cref="IngestAdjunctRequest"/>.
    /// </summary>
    /// <returns>Result of ingest operations</returns>
    Task<IngestResult> Ingest(IngestAdjunctRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for operations related to ingesting adjuncts.
/// This is an interface to allow post-processing work to be done, after adjunct has been saved to database.
/// </summary>
/// <remarks>
/// Currently there are no post-processors defined, hence this interface remains unimplemented
/// as a hook for future functionality
/// </remarks>
public interface IAdjunctIngesterPostProcess
{
    /// <summary>
    /// Carry out post-ingest operations
    /// </summary>
    Task PostIngest(AdjunctIngestionContext ingestionContext, bool ingestSuccessful);
}

public class AdjunctIngester(
    ICustomerOriginStrategyRepository customerOriginRepository,
    ICustomerAwsContext customerAwsContext,
    IngestExecutor executor,
    IEngineAssetRepository engineAssetRepository,
    ILogger<AdjunctIngester> logger)
    : DeliverableIngester(customerOriginRepository, customerAwsContext), IAdjunctIngester
{
    public async Task<IngestResult> Ingest(IngestAdjunctRequest request, CancellationToken cancellationToken = default)
    {
        // Scope any AWS requests made during this ingest to the customer that owns the asset
        using var customerAwsScope = SetCustomerAwsContext(request.AssetId);

        var adjunct = await engineAssetRepository.GetAdjunct(request.Id, request.AssetId, request.BatchId, cancellationToken);
        
        if (adjunct == null)
        {
            logger.LogError("Could not find an adjunct for adjunct id {AdjunctId}, asset id {AssetId}", request.Id,
                request.AssetId);
            return new IngestResult(null, IngestResultStatus.Failed);
        }
        
        // get any matching CustomerOriginStrategy
        var customerOriginStrategy = await GetCustomerOriginStrategy(adjunct);

        // now ingest the adjunct
        var status = await executor.IngestAdjunct(adjunct, customerOriginStrategy, cancellationToken);
        return status;
    }
}

public class AdjunctIngestResult(string adjunctId, AssetId? assetId, IngestResultStatus ingestResult)
    : IngestResult(assetId, ingestResult)
{
    public string AdjunctId  { get; } = adjunctId;
}
