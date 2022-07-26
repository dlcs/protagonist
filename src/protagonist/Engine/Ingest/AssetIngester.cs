using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using DLCS.Model.Policies;
using Engine.Ingest.Models;
using Engine.Ingest.Workers;

namespace Engine.Ingest;

/// <summary>
/// Delegate for getting the correct <see cref="IAssetIngesterWorker"/> for specified Family.
/// </summary>
/// <param name="family">The type of ingester worker.</param>
public delegate IAssetIngesterWorker IngestorResolver(AssetFamily family);

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
    private readonly IngestorResolver resolver;
    private readonly ILogger<AssetIngester> logger;
    private readonly ICustomerOriginStrategyRepository customerOriginRepository;
    private readonly IPolicyRepository policyRepository;

    public AssetIngester(
        IPolicyRepository policyRepository, 
        ICustomerOriginStrategyRepository customerOriginRepository,
        ILogger<AssetIngester> logger,
        IngestorResolver resolver)
    {
        this.policyRepository = policyRepository;
        this.customerOriginRepository = customerOriginRepository;
        this.logger = logger;
        this.resolver = resolver;
    }

    /// <summary>
    /// Run ingest based on <see cref="LegacyIngestEvent"/>.
    /// </summary>
    /// <returns>Result of ingest operations</returns>
    /// <remarks>This is to comply with message format sent by Deliverator API.</remarks>
    public Task<IngestResult> Ingest(LegacyIngestEvent request, CancellationToken cancellationToken = default)
    {
        try
        {
            var internalIngestRequest = request.ConvertToAssetRequest();
            return Ingest(internalIngestRequest, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception ingesting IncomingIngest - {Message}", request.Message);
            return Task.FromResult(IngestResult.Failed);
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
            
        // get the relevant resolver (Image or Timebased)
        var ingestor = resolver(request.Asset.Family ?? AssetFamily.Image);

        return await ingestor.Ingest(request, customerOriginStrategy, cancellationToken);
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
            var optimisationPolicy = await policyRepository.GetImageOptimisationPolicy(asset.ImageOptimisationPolicy);
            asset.WithImageOptimisationPolicy(optimisationPolicy);
        }
    }
}