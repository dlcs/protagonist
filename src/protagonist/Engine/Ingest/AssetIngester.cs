using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using DLCS.Model.Policies;
using Engine.Ingest.Models;

namespace Engine.Ingest;

public interface IAssetIngester
{
    /// <summary>
    /// Run ingest based on <see cref="LegacyIngestEvent"/>.
    /// </summary>
    /// <returns>Result of ingest operations</returns>
    /// <remarks>This is to comply with message format sent by Deliverator API.</remarks>
    Task<IngestResult> Ingest(LegacyIngestEvent request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Run ingest based on <see cref="IngestAssetRequest"/>.
    /// </summary>
    /// <returns>Result of ingest operations</returns>
    Task<IngestResult> Ingest(IngestAssetRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains operations for ingesting assets.
/// </summary>
public class AssetIngester : IAssetIngester
{
    private readonly IngestExecutor executor;
    private readonly ILogger<AssetIngester> logger;
    private readonly ICustomerOriginStrategyRepository customerOriginRepository;
    private readonly IPolicyRepository policyRepository;

    public AssetIngester(
        IPolicyRepository policyRepository, 
        ICustomerOriginStrategyRepository customerOriginRepository,
        ILogger<AssetIngester> logger,
        IngestExecutor executor)
    {
        this.policyRepository = policyRepository;
        this.customerOriginRepository = customerOriginRepository;
        this.logger = logger;
        this.executor = executor;
    }

    /// <summary>
    /// Run ingest based on <see cref="LegacyIngestEvent"/>.
    /// </summary>
    /// <returns>Result of ingest operations</returns>
    /// <remarks>This is to comply with message format sent by Deliverator API.</remarks>
    public Task<IngestResult> Ingest(LegacyIngestEvent request, CancellationToken cancellationToken = default)
    {
        IngestAssetRequest? internalIngestRequest = null;
        try
        {
            internalIngestRequest = request.ConvertToAssetRequest();
            return Ingest(internalIngestRequest, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception ingesting IncomingIngest - {Message}", request.Message);
            return Task.FromResult(new IngestResult(internalIngestRequest?.Asset, IngestResultStatus.Failed));
        }
    }

    /// <summary>
    /// Run ingest based on <see cref="IngestAssetRequest"/>.
    /// </summary>
    /// <returns>Result of ingest operations</returns>
    public async Task<IngestResult> Ingest(IngestAssetRequest request, CancellationToken cancellationToken = default)
    {
        // get any matching CustomerOriginStrategy 
        var customerOriginStrategy = await customerOriginRepository.GetCustomerOriginStrategy(request.Asset, true);
            
        // set Thumbnail and ImageOptimisation policies on Asset
        await HydrateAssetPolicies(request.Asset);

        // now ingest the asset
        var status = await executor.IngestAsset(request.Asset, customerOriginStrategy, cancellationToken);
        return status;
    }

    private async Task HydrateAssetPolicies(Asset asset)
    {
        if (!string.IsNullOrEmpty(asset.ThumbnailPolicy))
        {
            var thumbnailPolicy = await policyRepository.GetThumbnailPolicy(asset.ThumbnailPolicy);
            asset.WithThumbnailPolicy(thumbnailPolicy);
        }

        if (!string.IsNullOrEmpty(asset.ImageOptimisationPolicy))
        {
            var optimisationPolicy =
                await policyRepository.GetImageOptimisationPolicy(asset.ImageOptimisationPolicy, asset.Customer);
            asset.WithImageOptimisationPolicy(optimisationPolicy);
        }
    }
}

public class IngestResult
{
    public Asset? Asset { get; }
    public IngestResultStatus Status { get; }

    public IngestResult(Asset? asset, IngestResultStatus ingestResult)
    {
        Asset = asset;
        Status = ingestResult;
    }
}