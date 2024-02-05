using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Model.DeliveryChannels;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.DeliveryChannels;

public class DefaultDeliveryChannelRepository : IDefaultDeliveryChannelRepository
{
    private readonly IAppCache appCache;
    private readonly CacheSettings cacheSettings;
    private readonly ILogger<DefaultDeliveryChannelRepository> logger;
    private readonly IDeliveryChannelPolicyRepository deliveryChannelPolicyRepository;
    private readonly DlcsContext dlcsContext;
    private const int SystemCustomerId = 1;
    private const int SystemSpaceId = 0;

    public DefaultDeliveryChannelRepository(
        IAppCache appCache,
        ILogger<DefaultDeliveryChannelRepository> logger,
        IOptions<CacheSettings> cacheOptions,
        IDeliveryChannelPolicyRepository deliveryChannelPolicyRepository,
        DlcsContext dlcsContext)
    {
        this.appCache = appCache;
        this.logger = logger;
        cacheSettings = cacheOptions.Value;
        this.deliveryChannelPolicyRepository = deliveryChannelPolicyRepository;
        this.dlcsContext = dlcsContext;
    }
    
    public async Task<bool> AddCustomerDefaultDeliveryChannels(int customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var defaultDeliveryChannelsToCopy = await GetDefaultDeliveryChannelsForSystemCustomer(cancellationToken);

            var updatedPolicies = defaultDeliveryChannelsToCopy.Select(async defaultDeliveryChannel => new DefaultDeliveryChannel()
            {
                Id = Guid.NewGuid(), 
                DeliveryChannelPolicyId = await GetCorrectDeliveryChannelId(customerId, defaultDeliveryChannel, cancellationToken), 
                MediaType = defaultDeliveryChannel.MediaType,
                Customer = customerId,
                Space = defaultDeliveryChannel.Space
                
            }).Select(t => t.Result).ToList();
            
            await dlcsContext.DefaultDeliveryChannels.AddRangeAsync(updatedPolicies, cancellationToken);
            
            await dlcsContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error adding delivery channel policies to customer {Customer}", customerId);
            return false;
        }

        return true;
    }
    
    private async Task<List<DefaultDeliveryChannel>> GetDefaultDeliveryChannelsForSystemCustomer(CancellationToken cancellationToken)
    {
        const string key = "DefaultDeliveryChannels";
        return await appCache.GetOrAddAsync(key, async () =>
        {
            logger.LogDebug("Refreshing DefaultDeliveryChannels from database");
            var defaultDeliveryChannels =
                await dlcsContext.DefaultDeliveryChannels.AsNoTracking().Where(p => p.Customer == SystemCustomerId && p.Space == SystemSpaceId).ToListAsync(cancellationToken: cancellationToken);
            return defaultDeliveryChannels;
        }, cacheSettings.GetMemoryCacheOptions());
    }

    private async Task<int> GetCorrectDeliveryChannelId(int customerId, DefaultDeliveryChannel defaultDeliveryChannel, CancellationToken cancellationToken)
    {
        int deliveryChannelPolicyId;

        switch (defaultDeliveryChannel.MediaType)
        {
            case "audio/*":
                var audioPolicy = await deliveryChannelPolicyRepository.GetDeliveryChannelPolicy(customerId,
                    "default-audio", "iiif-av", cancellationToken);
                deliveryChannelPolicyId = audioPolicy!.Id;
                break;
            case "video/*":
                var videoPolicy = await deliveryChannelPolicyRepository.GetDeliveryChannelPolicy(customerId,
                    "default-video", "iiif-av", cancellationToken);
                deliveryChannelPolicyId = videoPolicy!.Id;
                break;
            case "image/*":
                deliveryChannelPolicyId = await GetPolicyForImageMediaType(customerId, defaultDeliveryChannel, cancellationToken);
                break;
            default:
                deliveryChannelPolicyId = defaultDeliveryChannel.DeliveryChannelPolicyId;
                break;
        }

        return deliveryChannelPolicyId;
    }

    private async Task<int> GetPolicyForImageMediaType(int customerId, DefaultDeliveryChannel defaultDeliveryChannel,
        CancellationToken cancellationToken)
    {
        int deliveryChannelPolicyId;
        if (defaultDeliveryChannel.DeliveryChannelPolicyId != 1) // 1 has to be a iiif-img policy for a customer
        {
            var thumbsPolicy = await deliveryChannelPolicyRepository.GetDeliveryChannelPolicy(customerId,
                "default", "thumbs", cancellationToken);
            deliveryChannelPolicyId = thumbsPolicy!.Id;
        }
        else
        {
            deliveryChannelPolicyId = defaultDeliveryChannel.DeliveryChannelPolicyId;
        }

        return deliveryChannelPolicyId;
    }
}