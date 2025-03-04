using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using DLCS.Model.Policies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;
using ImageApi = IIIF.ImageApi;

namespace Orchestrator.Infrastructure;

public interface IThumbSizeProvider
{
    /// <summary>
    /// Get all sizes for thumbnails
    /// </summary>
    Task<ThumbnailSizes> GetThumbSizesForImage(Asset asset, CancellationToken cancellationToken = default);
}

public class MetadataWithFallbackThumbSizeProvider : IThumbSizeProvider
{
    private readonly IPolicyRepository policyRepository;
    private readonly ILogger<MetadataWithFallbackThumbSizeProvider> logger;
    private readonly OrchestratorSettings orchestratorSettings;
    private readonly Dictionary<int, List<ImageApi.SizeParameter>> thumbnailPolicies = new();

    public MetadataWithFallbackThumbSizeProvider(IPolicyRepository policyRepository, 
        IOptions<OrchestratorSettings> orchestratorOptions,
        ILogger<MetadataWithFallbackThumbSizeProvider> logger)
    {
        this.policyRepository = policyRepository;
        this.logger = logger;
        orchestratorSettings = orchestratorOptions.Value;
    }
    
    /// <summary>
    /// Get available sizes for thumbnails
    /// 
    /// This will _not_ hit S3 to read available thumbs, it will:
    /// Attempt to read from asset.AssetApplicationMetadata. If found return. Else
    /// Get thumbnail policy and calculate sizes. 
    /// </summary>
    public async Task<ThumbnailSizes> GetThumbSizesForImage(Asset asset, CancellationToken cancellationToken = default)
    {
        var thumbnailSizes = asset.AssetApplicationMetadata?.GetThumbsMetadata();
        
        if (thumbnailSizes != null)
        {
            logger.LogDebug("ThumbSizes metadata found for {AssetId}", asset.Id);
            return thumbnailSizes;
        }

        if ((orchestratorSettings.ThumbsMetadataDate ?? DateTime.MaxValue) < asset.Finished)
        {
            logger.LogWarning(
                "No thumbs metadata found for asset {AssetId} with finished date {FinishedDate}", asset.Id,
                asset.Finished);
        }
        
        return await GetThumbnailSizesForImage(asset, cancellationToken);
    }

    private async Task<ThumbnailSizes> GetThumbnailSizesForImage(Asset asset, CancellationToken cancellationToken)
    {
        logger.LogDebug("Calculating thumbnail sizes for {AssetId}", asset.Id);
        var sizeParameters = await GetThumbnailPolicyAsSizeParams(asset, cancellationToken);
        
        if (sizeParameters.IsNullOrEmpty()) return ThumbnailSizes.Empty;

        var thumbnailSizesForImage = asset.GetAvailableThumbSizes(sizeParameters);
        return thumbnailSizesForImage;
    }

    private async Task<List<ImageApi.SizeParameter>?> GetThumbnailPolicyAsSizeParams(Asset image, CancellationToken cancellationToken)
    {
        var thumbnailDeliveryChannel = image.ImageDeliveryChannels.GetThumbsChannel();

        if (thumbnailDeliveryChannel is null) return null;

        if (thumbnailPolicies.TryGetValue(thumbnailDeliveryChannel.DeliveryChannelPolicyId, out var thumbnailPolicy))
        {
            return thumbnailPolicy;
        }

        var thumbnailPolicyFromDb =
            await policyRepository.GetThumbnailPolicy(thumbnailDeliveryChannel.DeliveryChannelPolicyId, image.Customer,
                cancellationToken);

        var sizeParameters = thumbnailPolicyFromDb
            .ThrowIfNull(nameof(thumbnailPolicyFromDb))
            .ThumbsDataAsSizeParameters();

        if (!orchestratorSettings.ImageIngest.DefaultThumbs.IsNullOrEmpty())
        {
            var defaultThumbs = orchestratorSettings.ImageIngest.DefaultThumbs.Select(ImageApi.SizeParameter.Parse);
            sizeParameters = sizeParameters.Union(defaultThumbs).ToList();
        }
        
        thumbnailPolicies[thumbnailDeliveryChannel.DeliveryChannelPolicyId] = sizeParameters;
        return sizeParameters;
    }
}
