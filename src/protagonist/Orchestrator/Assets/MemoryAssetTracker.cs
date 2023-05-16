﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using LazyCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Orchestrator.Assets;

/// <summary>
/// <see cref="IAssetTracker"/> implementation using in-memory tracking
/// </summary>
public class MemoryAssetTracker : IAssetTracker
{
    private readonly IAssetRepository assetRepository;
    private readonly IAppCache appCache;
    private readonly CacheSettings cacheSettings;
    private readonly IThumbRepository thumbRepository;
    private readonly ICustomerOriginStrategyRepository customerOriginStrategyRepository;
    private readonly ILogger<MemoryAssetTracker> logger;

    // Null object to store in cache for short duration
    private static readonly OrchestrationAsset NullOrchestrationAsset =
        new() { AssetId = new AssetId(-1, -1, "__notfound__") };

    public MemoryAssetTracker(
        IAssetRepository assetRepository,
        IAppCache appCache,
        IThumbRepository thumbRepository,
        ICustomerOriginStrategyRepository customerOriginStrategyRepository,
        IOptions<CacheSettings> cacheOptions,
        ILogger<MemoryAssetTracker> logger)
    {
        this.assetRepository = assetRepository;
        this.appCache = appCache;
        this.thumbRepository = thumbRepository;
        this.customerOriginStrategyRepository = customerOriginStrategyRepository;
        this.logger = logger;
        cacheSettings = cacheOptions.Value;
    }

    public async Task<OrchestrationAsset?> GetOrchestrationAsset(AssetId assetId)
    {
        var trackedAsset = await GetOrchestrationAssetInternal(assetId);
        return IsNullAsset(trackedAsset) ? null : trackedAsset;
    }

    public async Task<T?> GetOrchestrationAsset<T>(AssetId assetId) where T : OrchestrationAsset
    {
        var trackedAsset = await GetOrchestrationAssetInternal(assetId);
        if (IsNullAsset(trackedAsset)) return null;

        if (trackedAsset is T typedAsset) return typedAsset;
        
        logger.LogWarning("Request for asset {AssetId} is of wrong type. Expected '{Expected}' but found '{Actual}",
            assetId, typeof(T), trackedAsset.GetType());
        return null;
    }
    
    public async Task<T?> RefreshCachedAsset<T>(AssetId assetId)
        where T : OrchestrationAsset
    {
        var cacheKey = GetCacheKey(assetId);

        var newOrchestrationAsset = await GetOrchestrationAssetFromSource(assetId);
        appCache.Add(cacheKey, newOrchestrationAsset, cacheSettings.GetMemoryCacheOptions());

        return newOrchestrationAsset as T;
    }

    private async Task<OrchestrationAsset> GetOrchestrationAssetInternal(AssetId assetId)
    {
        var key = GetCacheKey(assetId);
        return await appCache.GetOrAddAsync(key, async entry =>
        {
            logger.LogTrace("Refreshing cache for {AssetId}", assetId);
            var orchestrationAsset = await GetOrchestrationAssetFromSource(assetId);
            if (orchestrationAsset != null)
            {
                return orchestrationAsset;
            }
            
            // TODO - do we really care about caching non-images?

            logger.LogDebug("Asset {AssetId} not found, caching null object", assetId);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
            return NullOrchestrationAsset;
        }, cacheSettings.GetMemoryCacheOptions());
    }

    private async Task<OrchestrationAsset?> GetOrchestrationAssetFromSource(AssetId assetId)
    {
        var asset = await assetRepository.GetAsset(assetId);
        return asset == null || asset.NotForDelivery
            ? null
            : await ConvertAssetToTrackedAsset(assetId, asset);
    }

    private static string GetCacheKey(AssetId assetId) => $"Track:{assetId}";

    private bool IsNullAsset(OrchestrationAsset orchestrationAsset)
        => orchestrationAsset.AssetId == NullOrchestrationAsset.AssetId;

    private async Task<OrchestrationAsset> ConvertAssetToTrackedAsset(AssetId assetId, Asset asset)
    {
        T SetDefaults<T>(T orchestrationAsset)
            where T : OrchestrationAsset
        {
            if (asset.HasDeliveryChannel(AssetDeliveryChannels.File))
                orchestrationAsset.Channels |= AvailableDeliveryChannel.File;
            if (asset.HasDeliveryChannel(AssetDeliveryChannels.Image))
                orchestrationAsset.Channels |= AvailableDeliveryChannel.Image;
            if (asset.HasDeliveryChannel(AssetDeliveryChannels.Timebased))
                orchestrationAsset.Channels |= AvailableDeliveryChannel.Timebased;
            
            orchestrationAsset.AssetId = assetId;
            orchestrationAsset.Roles = asset.RolesList.ToList();
            orchestrationAsset.RequiresAuth = asset.RequiresAuth;
            return orchestrationAsset;
        }

        OrchestrationAsset orchestrationAsset;
        
        if (asset.HasDeliveryChannel(AssetDeliveryChannels.Image))
        {
            var getImageLocation = assetRepository.GetImageLocation(assetId);
            var getOpenThumbs = thumbRepository.GetOpenSizes(assetId);

            await Task.WhenAll(getImageLocation, getOpenThumbs);
            
            orchestrationAsset = new OrchestrationImage
            {
                S3Location = getImageLocation.Result?.S3, // TODO - error handling
                Width = asset.Width ?? 0,
                Height = asset.Height ?? 0,
                MaxUnauthorised = asset.MaxUnauthorised ?? 0,
                OpenThumbs = getOpenThumbs.Result ?? new List<int[]>(), // TODO - reorganise thumb layout + create missing eventually
            };
        }
        else
        {
            orchestrationAsset = new OrchestrationAsset();
        }

        if (asset.HasDeliveryChannel(AssetDeliveryChannels.File))
        {
            var origin = asset.Origin.ThrowIfNullOrEmpty(nameof(asset.Origin));
            var cos = await customerOriginStrategyRepository.GetCustomerOriginStrategy(assetId, origin);
            orchestrationAsset.Origin = origin;
            orchestrationAsset.OptimisedOrigin = cos.Optimised;
            orchestrationAsset.MediaType = new StringValues(asset.MediaType ?? "application/octet-stream");
        }
        
        return SetDefaults(orchestrationAsset);
    }
}