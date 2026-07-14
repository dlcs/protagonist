using System;
using System.Linq;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.Core.Caching;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using IIIF;
using LazyCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Orchestrator.Infrastructure.DataAccess;
using Orchestrator.Settings;

namespace Orchestrator.Assets;

/// <summary>
/// <see cref="IAssetTracker"/> implementation using in-memory tracking
/// </summary>
public class MemoryAssetTracker(
    IOrchestratorAssetRepository assetRepository,
    IOrchestratorAdjunctRepository adjunctRepository,
    IAppCache appCache,
    IThumbRepository thumbRepository,
    ICustomerOriginStrategyRepository customerOriginStrategyRepository,
    IOptions<OrchestratorSettings> orchestratorOptions,
    ILogger<MemoryAssetTracker> logger)
    : IAssetTracker, IAdjunctTracker
{
    private readonly CacheSettings cacheSettings = orchestratorOptions.Value.Caching;
    private readonly ReingestOnOrchestrationSettings reingestSettings = orchestratorOptions.Value.ReingestOnOrchestration;
    private readonly int systemMaxWidth = orchestratorOptions.Value.MaxWidth;

    // Null objects to store in cache for short duration
    private static readonly OrchestrationAsset NullOrchestrationAsset =
        new() { AssetId = new AssetId(-1, -1, "__notfound__") };

    private static readonly OrchestrationAdjunct NullOrchestrationAdjunct =
        new() { Id = "__missingadjunct__", AssetId = new AssetId(-1, -1, "__notfound__") };

    public async Task<OrchestrationAsset?> GetOrchestrationAsset(AssetId assetId)
    {
        var trackedAsset = await GetOrchestrationAssetInternal(assetId);
        return IsNullItem(trackedAsset) ? null : trackedAsset;
    }

    public async Task<T?> GetOrchestrationAsset<T>(AssetId assetId) where T : OrchestrationAsset
    {
        var trackedAsset = await GetOrchestrationAssetInternal(assetId);
        if (IsNullItem(trackedAsset)) return null;

        if (trackedAsset is T typedAsset) return typedAsset;
        
        logger.LogWarning("Request for asset {AssetId} is of wrong type. Expected '{Expected}' but found '{Actual}",
            assetId, typeof(T), trackedAsset.GetType());
        return null;
    }
    
    public async Task<T?> RefreshCachedAsset<T>(AssetId assetId)
        where T : OrchestrationAsset
    {
        var cacheKey = GetCacheKey(assetId);

        var newOrchestrationAsset = await GetOrchestrationAssetFromSource(assetId, true);
        appCache.Add(cacheKey, newOrchestrationAsset,
            cacheSettings.GetMemoryCacheOptions(named: CacheOverrideKeys.OrchestrationAsset));

        return newOrchestrationAsset as T;
    }
    
    public async Task<OrchestrationAdjunct?> RefreshCachedAdjunct(string adjunctId, AssetId assetId)
    {
        var cacheKey = GetCacheKey(assetId, adjunctId);

        var newOrchestrationAsset = await GetOrchestrationAdjunctFromSource(adjunctId, assetId, true);
        appCache.Add(cacheKey, newOrchestrationAsset,
            cacheSettings.GetMemoryCacheOptions(named: CacheOverrideKeys.OrchestrationAdjunct));

        return newOrchestrationAsset;
    }
    
    public async Task<OrchestrationAdjunct?> GetOrchestrationAdjunct(string adjunctId, AssetId assetId)
    {
        var trackedAdjunct = await GetOrchestrationAdjunctInternal(adjunctId, assetId);
        return IsNullItem(trackedAdjunct) ? null : trackedAdjunct;
    }

    private async Task<OrchestrationAdjunct> GetOrchestrationAdjunctInternal(string adjunctId, AssetId assetId)
    {
        var key = GetCacheKey(assetId, adjunctId);
        return await appCache.GetOrAddAsync(key, async entry =>
        {
            logger.LogTrace("Refreshing cache for {AssetId}", assetId);
            var orchestrationAdjunct = await GetOrchestrationAdjunctFromSource(adjunctId, assetId);

            if (orchestrationAdjunct != null)
            {
                return orchestrationAdjunct;
            }

            logger.LogDebug("Adjunct {Id} for asset {AssetId} not found, caching null object", adjunctId, assetId);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
            return NullOrchestrationAdjunct;

        }, cacheSettings.GetMemoryCacheOptions(named: CacheOverrideKeys.OrchestrationAdjunct));
    }
    
    private async Task<OrchestrationAsset> GetOrchestrationAssetInternal(AssetId assetId)
    {
        var key = GetCacheKey(assetId);
        return await appCache.GetOrAddAsync(key, async entry =>
        {
            logger.LogTrace("Refreshing cache for {AssetId}", assetId);
            var orchestrationAsset = await GetOrchestrationAssetFromSource(assetId);

            if (orchestrationAsset == null)
            {
                logger.LogDebug("Asset {AssetId} not found, caching null object", assetId);
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
                return NullOrchestrationAsset;
            }

            if (orchestrationAsset is OrchestrationImage orchestrationImage && orchestrationImage.IsNotFound())
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSettings.GetTtl(CacheDuration.Short));
            }

            return orchestrationAsset;

        }, cacheSettings.GetMemoryCacheOptions(named: CacheOverrideKeys.OrchestrationAsset));
    }

    private async Task<OrchestrationAdjunct?> GetOrchestrationAdjunctFromSource(string adjunctId, AssetId assetId, bool noCache = false)
    {
        var asset = await adjunctRepository.GetAdjunct(adjunctId, assetId, noCache);
        return asset == null
            ? null
            : await ConvertAdjunctToTrackedAdjunct(assetId, asset);
    }
    
    private async Task<OrchestrationAsset?> GetOrchestrationAssetFromSource(AssetId assetId, bool noCache = false)
    {
        var asset = await assetRepository.GetAsset(assetId, noCache);
        return asset == null || asset.NotForDelivery
            ? null
            : await ConvertAssetToTrackedAsset(assetId, asset);
    }

    private static string GetCacheKey(AssetId assetId, string? adjunctId = null)
        => $"Track:{assetId}" + (adjunctId is { Length: > 0 }
            ? $"_adj_{adjunctId}"
            : string.Empty);

    private static bool IsNullItem<T>(T orchestrationItem)
        => orchestrationItem switch
        {
            OrchestrationAsset orchestrationAsset => orchestrationAsset.AssetId == NullOrchestrationAsset.AssetId,
            OrchestrationAdjunct orchestrationAdjunct => orchestrationAdjunct.AssetId ==
                NullOrchestrationAdjunct.AssetId && orchestrationAdjunct.Id == NullOrchestrationAdjunct.Id,
            _ => true // unsupported, definitely don't allow
        };

    private async Task<OrchestrationAdjunct> ConvertAdjunctToTrackedAdjunct(AssetId assetId, Adjunct adjunct)
    {
        var origin = adjunct.Origin.ThrowIfNullOrEmpty(nameof(adjunct.Origin));
        var cos = await customerOriginStrategyRepository.GetCustomerOriginStrategy(assetId, origin);
        var orchestrationAdjunct = new OrchestrationAdjunct
        {
            Id = adjunct.Id,
            AssetId = adjunct.AssetId,
            Origin = origin,
            IIIFLink = adjunct.IIIFLink,
            MediaType = new StringValues(adjunct.MediaType),
            OptimisedOrigin = cos.Optimised
        };
        
        return  orchestrationAdjunct;
    }
    
    private async Task<OrchestrationAsset> ConvertAssetToTrackedAsset(AssetId assetId, Asset asset)
    {
        OrchestrationAsset orchestrationAsset;
        
        if (asset.HasDeliveryChannel(AssetDeliveryChannels.Image))
        {
            var getImageLocation = assetRepository.GetImageLocation(assetId);
            var getOpenThumbs = thumbRepository.GetOpenSizes(assetId);

            await Task.WhenAll(getImageLocation, getOpenThumbs);

            var imageLocation = getImageLocation.Result;
            
            orchestrationAsset = new OrchestrationImage
            {
                S3Location = imageLocation?.S3,
                Size = new Size(asset.Width ?? 0, asset.Height ?? 0),
                MaxWidth = asset.GetEffectiveMaxWidth(systemMaxWidth),
                OpenFullMax = asset.HasRoles ? asset.OpenFullMax : null,
                OpenThumbs = getOpenThumbs.Result ?? [],
                Reingest = GetReingestFlag(asset, imageLocation),
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
        
        return SetDefaults();

        OrchestrationAsset SetDefaults()
        {
            if (asset.HasDeliveryChannel(AssetDeliveryChannels.File))
                orchestrationAsset.Channels |= AvailableDeliveryChannel.File;
            if (asset.HasDeliveryChannel(AssetDeliveryChannels.Image))
                orchestrationAsset.Channels |= AvailableDeliveryChannel.Image;
            if (asset.HasDeliveryChannel(AssetDeliveryChannels.Timebased))
                orchestrationAsset.Channels |= AvailableDeliveryChannel.Timebased;
            
            orchestrationAsset.AssetId = assetId;
            orchestrationAsset.Roles = asset.RolesList.ToList();
            orchestrationAsset.RequiresAuth = asset.HasRoles;
            return orchestrationAsset;
        }
    }
    
    private bool GetReingestFlag(Asset asset, ImageLocation? imageLocation)
    {
        // Reingest on the fly if no S3Location and image was created prior to EmptyImageLocationCreatedDate
        if (!string.IsNullOrEmpty(imageLocation?.S3)) return false;
        if (!reingestSettings.EmptyImageLocationCreatedDate.HasValue) return false;

        return (asset.Created ?? DateTime.UtcNow).Date <= reingestSettings.EmptyImageLocationCreatedDate.Value.Date;
    }
}
