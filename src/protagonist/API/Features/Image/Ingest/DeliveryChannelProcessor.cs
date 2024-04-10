using System.Collections.Generic;
using API.Exceptions;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.Policies;
using Microsoft.Extensions.Logging;

namespace API.Features.Image.Ingest;

public class DeliveryChannelProcessor
{
    private readonly IDefaultDeliveryChannelRepository defaultDeliveryChannelRepository;
    private readonly IDeliveryChannelPolicyRepository deliveryChannelPolicyRepository;
    private readonly ILogger<DeliveryChannelProcessor> logger;
    private const string FileNonePolicy = "none";

    public DeliveryChannelProcessor(IDefaultDeliveryChannelRepository defaultDeliveryChannelRepository,
        IDeliveryChannelPolicyRepository deliveryChannelPolicyRepository, ILogger<DeliveryChannelProcessor> logger)
    {
        this.defaultDeliveryChannelRepository = defaultDeliveryChannelRepository;
        this.deliveryChannelPolicyRepository = deliveryChannelPolicyRepository;
        this.logger = logger;
    }
    
    /// <summary>
    /// Update updatedAsset.ImageDeliveryChannels, adding/removing/updating as required to match channels specified in
    /// deliveryChannelsBeforeProcessing
    /// </summary>
    /// <param name="existingAsset">Existing asset, if found (will only be present for updates)</param>
    /// <param name="updatedAsset">
    /// Asset that is existing asset (if update) or default asset (if create) with changes applied.
    /// </param>
    /// <param name="deliveryChannelsBeforeProcessing">List of deliveryChannels submitted in body</param>
    /// <returns>Boolean indicating whether asset requires processing Engine</returns>
    public async Task<bool> ProcessImageDeliveryChannels(Asset? existingAsset, Asset updatedAsset,
        DeliveryChannelsBeforeProcessing[] deliveryChannelsBeforeProcessing)
    {
        if (existingAsset == null ||
            DeliveryChannelsRequireReprocessing(existingAsset, deliveryChannelsBeforeProcessing))
        {
            try
            {
                var deliveryChannelChanged = await SetImageDeliveryChannels(updatedAsset,
                    deliveryChannelsBeforeProcessing, existingAsset != null);
                return deliveryChannelChanged;
            }
            catch (InvalidOperationException)
            {
                throw new APIException("Failed to match delivery channel policy")
                {
                    StatusCode = 400
                };
            }
        }

        return false;
    }

    private bool DeliveryChannelsRequireReprocessing(Asset originalAsset, DeliveryChannelsBeforeProcessing[] deliveryChannelsBeforeProcessing)
    {
        if (originalAsset.ImageDeliveryChannels.Count != deliveryChannelsBeforeProcessing.Length) return true;
        
        foreach (var deliveryChannel in deliveryChannelsBeforeProcessing)
        {
            if (!originalAsset.ImageDeliveryChannels.Any(c =>
                    c.Channel == deliveryChannel.Channel &&
                    c.DeliveryChannelPolicy.Name == deliveryChannel.Policy))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> SetImageDeliveryChannels(Asset asset, DeliveryChannelsBeforeProcessing[] deliveryChannelsBeforeProcessing, bool isUpdate)
    {
        var assetId = asset.Id;
        
        if (!isUpdate)
        {
            logger.LogTrace("Asset {AssetId} is new, resetting ImageDeliveryChannels", assetId);
            asset.ImageDeliveryChannels = new List<ImageDeliveryChannel>();
            
            // Only valid for creation - set image delivery channels to default values for media type
            if (deliveryChannelsBeforeProcessing.IsNullOrEmpty())
            {
                logger.LogDebug("Asset {AssetId} is new, no deliveryChannels specified. Assigning defaults for mediaType",
                    assetId);
                await AddDeliveryChannelsForMediaType(asset);
                return true;
            }
        }

        // If 'none' specified then it's the only valid option
        if (deliveryChannelsBeforeProcessing.Count(d => d.Channel == AssetDeliveryChannels.None) == 1)
        {
            await AddExplicitNoneChannel(asset);
            return true;
        }

        // Iterate through DeliveryChannels specified in payload and make necessary update/delete/insert
        var changeMade = false;
        var handledChannels = new List<string>();
        var assetImageDeliveryChannels = asset.ImageDeliveryChannels;
        foreach (var deliveryChannel in deliveryChannelsBeforeProcessing)
        {
            handledChannels.Add(deliveryChannel.Channel);
            var deliveryChannelPolicy = await GetDeliveryChannelPolicy(asset, deliveryChannel);
            var currentChannel = assetImageDeliveryChannels.SingleOrDefault(idc => idc.Channel == deliveryChannel.Channel);

            // No current ImageDeliveryChannel for channel so this is an addition
            if (currentChannel == null)
            {
                logger.LogTrace("Adding new deliveryChannel {DeliveryChannel}, Policy {PolicyName} to Asset {AssetId}",
                    deliveryChannel.Channel, deliveryChannelPolicy.Name, assetId);

                assetImageDeliveryChannels.Add(new ImageDeliveryChannel
                {
                    ImageId = assetId,
                    DeliveryChannelPolicyId = deliveryChannelPolicy.Id,
                    Channel = deliveryChannel.Channel
                });
                changeMade = true;
            }
            else
            {
                // There is already a IDC for this thing - has the policy changed?
                if (currentChannel.DeliveryChannelPolicyId != deliveryChannelPolicy.Id)
                {
                    logger.LogTrace(
                        "Asset {AssetId} already has deliveryChannel {DeliveryChannel}, but policy changed from {OldPolicyName} to Asset {NewPolicyName}",
                        assetId, deliveryChannel.Channel, currentChannel.DeliveryChannelPolicy.Name,
                        deliveryChannelPolicy.Name);
                    currentChannel.DeliveryChannelPolicy = deliveryChannelPolicy;
                    changeMade = true;
                }
            }
        }

        if (isUpdate)
        {
            // Remove any that are no longer part of the payload
            foreach (var deletedChannel in assetImageDeliveryChannels.Where(idc =>
                         !handledChannels.Contains(idc.Channel)))
            {
                logger.LogTrace("Removing deliveryChannel {DeliveryChannel}, from Asset {AssetId}",
                    deletedChannel.Channel, assetId);
                assetImageDeliveryChannels.Remove(deletedChannel);
                changeMade = true;
            }
        }
            
        return changeMade;
    }

    private async Task<DeliveryChannelPolicy> GetDeliveryChannelPolicy(Asset asset, DeliveryChannelsBeforeProcessing deliveryChannel)
    {
        DeliveryChannelPolicy deliveryChannelPolicy;
        if (deliveryChannel.Policy.IsNullOrEmpty())
        {
            deliveryChannelPolicy = await defaultDeliveryChannelRepository.MatchDeliveryChannelPolicyForChannel(
                asset.MediaType!, asset.Space, asset.Customer, deliveryChannel.Channel);
        }
        else
        {
            deliveryChannelPolicy = await deliveryChannelPolicyRepository.RetrieveDeliveryChannelPolicy(
                asset.Customer,
                deliveryChannel.Channel,
                deliveryChannel.Policy);
        }

        return deliveryChannelPolicy;
    }

    private async Task AddExplicitNoneChannel(Asset asset)
    {
        logger.LogTrace("assigning 'none' channel for asset {AssetId}", asset.Id);
        var deliveryChannelPolicy = await deliveryChannelPolicyRepository.RetrieveDeliveryChannelPolicy(asset.Customer,
            AssetDeliveryChannels.None, FileNonePolicy);

        // "none" channel can only exist on it's own so remove any others that may be there already prior to adding
        asset.ImageDeliveryChannels.Clear();
        asset.ImageDeliveryChannels.Add(new ImageDeliveryChannel
        {
            ImageId = asset.Id,
            DeliveryChannelPolicyId = deliveryChannelPolicy.Id,
            Channel = AssetDeliveryChannels.None
        });
    }

    private async Task AddDeliveryChannelsForMediaType(Asset asset)
    {
        var matchedDeliveryChannels =
            await defaultDeliveryChannelRepository.MatchedDeliveryChannels(asset.MediaType!, asset.Space,
                asset.Customer);

        if (matchedDeliveryChannels.Any(x => x.Channel == AssetDeliveryChannels.None) &&
            matchedDeliveryChannels.Count != 1)
        {
            throw new APIException("An asset can only be automatically assigned a delivery channel of type 'None' when it is the only one available. " +
                                   "Please check your default delivery channel configuration.")
            {
                StatusCode = 400
            };
        }
        
        foreach (var deliveryChannel in matchedDeliveryChannels)
        {
            asset.ImageDeliveryChannels.Add(new ImageDeliveryChannel
            {
                ImageId = asset.Id,
                DeliveryChannelPolicyId = deliveryChannel.Id,
                Channel = deliveryChannel.Channel
            });
        }
    }
}