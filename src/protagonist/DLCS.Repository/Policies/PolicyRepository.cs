using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using DLCS.Model.Storage;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Policies;

public class PolicyRepository : IPolicyRepository
{
    private readonly IAppCache appCache;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<PolicyRepository> logger;
    private readonly DlcsContext dlcsContext;

    public PolicyRepository(
        IAppCache appCache,
        ILogger<PolicyRepository> logger,
        IOptions<CacheSettings> cacheOptions,
        DlcsContext dlcsContext)
    {
        this.appCache = appCache;
        this.logger = logger;
        cacheSettings = cacheOptions.Value;
        this.dlcsContext = dlcsContext;
    }
    
    public async Task<DeliveryChannelPolicy?> GetThumbnailPolicy(int deliveryChannelPolicyId, int customerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var thumbnailPolicies =
                await GetThumbnailDeliveryChannelPolicies(customerId, cancellationToken);
            return thumbnailPolicies.SingleOrDefault(p => p.Id == deliveryChannelPolicyId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting deliver channel policy with id {DeliveryChannelPolicyId}",
                deliveryChannelPolicyId);
            return null;
        }
    }

    public async Task<ImageOptimisationPolicy?> GetImageOptimisationPolicy(string imageOptimisationPolicyId,
        int customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var imageOptimisationPolicies = await GetImageOptimisationPolicies(cancellationToken);
            return imageOptimisationPolicies
                .OrderBy(c => c.Global)
                .FirstOrDefault(p => p.Id == imageOptimisationPolicyId && (p.Global || p.Customer == customerId));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting ImageOptimisationPolicy with id {ImageOptimisationPolicyId}",
                imageOptimisationPolicyId);
            return null;
        }
    }

    public async Task<StoragePolicy?> GetStoragePolicy(string storagePolicyId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var storagePolicies = await GetStoragePolicies(cancellationToken);
            return storagePolicies.SingleOrDefault(p => p.Id == storagePolicyId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting StoragePolicy with id {StoragePolicyId}",
                storagePolicyId);
            return null;
        }
    }

    private Task<List<StoragePolicy>> GetStoragePolicies(CancellationToken cancellationToken)
    {
        const string key = "StoragePolicies";
        return appCache.GetOrAddAsync(key, async () =>
        {
            logger.LogDebug("Refreshing StoragePolicies from database");
            var storagePolicies = await dlcsContext.StoragePolicies.AsNoTracking()
                .ToListAsync(cancellationToken: cancellationToken);
            return storagePolicies;
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Long));
    }

    private Task<List<ThumbnailPolicy>> GetThumbnailPolicies(CancellationToken cancellationToken)
    {
        const string key = "ThumbnailPolicies";
        return appCache.GetOrAddAsync(key, async () =>
        {
            logger.LogDebug("Refreshing ThumbnailPolicies from database");
            var thumbnailPolicies =
                await dlcsContext.ThumbnailPolicies.AsNoTracking().ToListAsync(cancellationToken: cancellationToken);
            return thumbnailPolicies;
        }, cacheSettings.GetMemoryCacheOptions());
    }
    
    private async Task<List<DeliveryChannelPolicy>> GetThumbnailDeliveryChannelPolicies(int customerId, CancellationToken cancellationToken)
    {
        string key = $"ThumbnailDeliveryChannelPolicies:{customerId}";
        return await appCache.GetOrAddAsync(key, async () =>
        {
            logger.LogDebug("Refreshing ThumbnailPolicies from database");
            var thumbnailPolicies =
                await dlcsContext.DeliveryChannelPolicies
                    .Where(d => d.Customer == customerId && d.Channel == AssetDeliveryChannels.Thumbnails)
                    .AsNoTracking().ToListAsync(cancellationToken: cancellationToken);
            return thumbnailPolicies;
        }, cacheSettings.GetMemoryCacheOptions());
    }
    
    private Task<List<ImageOptimisationPolicy>> GetImageOptimisationPolicies(CancellationToken cancellationToken)
    {
        const string key = "ImageOptimisation";
        return appCache.GetOrAddAsync(key, async () =>
        {
            logger.LogDebug("Refreshing ImageOptimisationPolicies from database");
            var imageOptimisationPolicies =
                await dlcsContext.ImageOptimisationPolicies.AsNoTracking()
                    .ToListAsync(cancellationToken: cancellationToken);
            return imageOptimisationPolicies;
        }, cacheSettings.GetMemoryCacheOptions());
    }
}