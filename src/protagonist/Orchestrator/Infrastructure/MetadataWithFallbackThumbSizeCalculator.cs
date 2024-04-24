using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using DLCS.Model.Policies;
using IIIF;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Settings;
using ImageApi = IIIF.ImageApi;

namespace Orchestrator.Infrastructure;

public interface IThumbSizeProvider
{
    /// <summary>
    /// Get available sizes for thumbnails (if any). ie it will only return "Open" thumb sizes
    /// </summary>
    Task<List<Size>> GetAvailableThumbSizesForImage(Asset asset);
}

public class MetadataWithFallbackThumbSizeProvider : IThumbSizeProvider
{
    private readonly IPolicyRepository policyRepository;
    private readonly ILogger<MetadataWithFallbackThumbSizeProvider> logger;
    private readonly OrchestratorSettings orchestratorSettings;
    private readonly Dictionary<int, List<ImageApi.SizeParameter>> thumbnailPolicies = new();

    public MetadataWithFallbackThumbSizeProvider(IPolicyRepository policyRepository, 
        ILogger<MetadataWithFallbackThumbSizeProvider> logger,
        IOptions<OrchestratorSettings> orchestratorOptions)
    {
        this.policyRepository = policyRepository;
        this.logger = logger;
        orchestratorSettings = orchestratorOptions.Value;
    }
    
    /// <summary>
    /// Get available sizes for thumbnails (if any). ie it will only return "Open" thumb sizes
    ///
    /// This will _not_ hit S3 to read available thumbs, it will:
    /// Attempt to read from asset.AssetApplicationMetadata. If found return. Else
    /// Get thumbnail policy and calculate available sizes. 
    /// </summary>
    public async Task<List<Size>> GetAvailableThumbSizesForImage(Asset asset)
    {
        var thumbnailSizes = asset.AssetApplicationMetadata?.GetThumbsMetadata();
        
        if (thumbnailSizes != null)
        {
            logger.LogDebug("ThumbSizes metadata found for {AssetId}", asset.Id);
            return thumbnailSizes.Open.Select(t => new Size(t[0], t[1])).ToList();
        }

        if ((orchestratorSettings.ThumbsMetadataDate ?? DateTime.MaxValue) < asset.Finished)
        {
            logger.LogWarning(
                "No thumbs metadata found for asset {AssetId} with finished date {FinishedDate}", asset.Id,
                asset.Finished);
        }
        
        return await GetThumbnailSizesForImage(asset) ?? Enumerable.Empty<Size>().ToList();
    }

    private async Task<List<Size>?> GetThumbnailSizesForImage(Asset asset)
    {
        logger.LogDebug("Calculating thumbnail sizes for {AssetId}", asset.Id);
        var sizeParameters = await GetThumbnailPolicyAsSizeParams(asset);

        var thumbnailSizesForImage = asset.GetAvailableThumbSizes(sizeParameters, out _);
        return thumbnailSizesForImage;
    }

    private async Task<List<ImageApi.SizeParameter>> GetThumbnailPolicyAsSizeParams(Asset image)
    {
        var thumbnailDeliveryChannel = image.ImageDeliveryChannels.GetThumbsChannel();

        if (thumbnailDeliveryChannel is null) return Enumerable.Empty<ImageApi.SizeParameter>().ToList();

        if (thumbnailPolicies.TryGetValue(thumbnailDeliveryChannel.DeliveryChannelPolicyId, out var thumbnailPolicy))
        {
            return thumbnailPolicy;
        }

        var thumbnailPolicyFromDb =
            await policyRepository.GetThumbnailPolicy(thumbnailDeliveryChannel.DeliveryChannelPolicyId, image.Customer);

        var sizeParameters = thumbnailPolicyFromDb
            .ThrowIfNull(nameof(thumbnailPolicyFromDb))
            .ThumbsDataAsSizeParameters();
        thumbnailPolicies[thumbnailDeliveryChannel.DeliveryChannelPolicyId] = sizeParameters;
        return sizeParameters;
    }
}