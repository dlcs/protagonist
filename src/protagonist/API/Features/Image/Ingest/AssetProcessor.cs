using System.Collections.Generic;
using System.IO.Enumeration;
using API.Features.Assets;
using API.Infrastructure.Requests;
using API.Settings;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Core.Settings;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.Policies;
using DLCS.Model.Storage;
using Microsoft.Extensions.Options;

namespace API.Features.Image.Ingest;

/// <summary>
/// Class that encapsulates logic for creating or updating assets.
/// The logic here is shared for when ingesting a single asset and ingesting a batch of assets.
/// </summary>
public class AssetProcessor
{
    private readonly IPolicyRepository policyRepository;
    private readonly IApiAssetRepository assetRepository;
    private readonly IStorageRepository storageRepository;
    private readonly IDefaultDeliveryChannelRepository defaultDeliveryChannelRepository;
    private readonly ApiSettings settings;
    
    public AssetProcessor(
        IApiAssetRepository assetRepository,
        IStorageRepository storageRepository,
        IPolicyRepository policyRepository,
        IDefaultDeliveryChannelRepository defaultDeliveryChannelRepository,
        IOptionsMonitor<ApiSettings> apiSettings)
    {
        this.assetRepository = assetRepository;
        this.storageRepository = storageRepository;
        this.policyRepository = policyRepository;
        this.defaultDeliveryChannelRepository = defaultDeliveryChannelRepository;
        settings = apiSettings.CurrentValue;
    }

    /// <summary>
    /// Process an asset - including validation and handling Update or Insert logic and get ready for ingestion
    /// </summary>
    /// <param name="asset">Asset sent to API</param>
    /// <param name="mustExist">If true, then only Update operations are supported</param>
    /// <param name="alwaysReingest">If true, then engine will be notified</param>
    /// <param name="isBatchUpdate">
    /// If true, this operation is part of a batch save. Allows Batch property to be set
    /// </param>
    /// <param name="requiresReingestPreSave">Optional delegate for modifying asset prior to saving</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <param name="isPriorityQueue">Whether the request is for the priority queue or not</param>
    public async Task<ProcessAssetResult> Process(Asset asset, bool mustExist, bool alwaysReingest, bool isBatchUpdate, 
        Func<Asset, Task>? requiresReingestPreSave = null, 
        CancellationToken cancellationToken = default)
    {
        Asset? existingAsset;
        try
        {
            existingAsset = await assetRepository.GetAsset(asset.Id, noCache: true);
            if (existingAsset == null)
            {
                if (mustExist)
                {
                    return new ProcessAssetResult
                    {
                        Result = ModifyEntityResult<Asset>.Failure(
                            "Attempted to update an Asset that could not be found",
                            WriteResult.NotFound
                        )
                    };
                }

                var counts = await storageRepository.GetStorageMetrics(asset.Customer, cancellationToken);
                if (!counts.CanStoreAsset())
                {
                    return new ProcessAssetResult
                    {
                        Result = ModifyEntityResult<Asset>.Failure(
                            $"This operation will fall outside of your storage policy for number of images: maximum is {counts.MaximumNumberOfStoredImages}",
                            WriteResult.StorageLimitExceeded
                        )
                    };
                }
                
                if (!counts.CanStoreAssetSize(0,0))
                {
                    return new ProcessAssetResult
                    {
                        Result = ModifyEntityResult<Asset>.Failure(
                            $"The total size of stored images has exceeded your allowance: maximum is {counts.MaximumTotalSizeOfStoredImages}",
                            WriteResult.StorageLimitExceeded
                        )
                    };
                }

                counts.CustomerStorage.NumberOfStoredImages++;
            }

            var assetPreparationResult =
                AssetPreparer.PrepareAssetForUpsert(existingAsset, asset, false, isBatchUpdate, settings.RestrictedAssetIdCharacters);

            if (!assetPreparationResult.Success)
            {
                return new ProcessAssetResult
                {
                    Result = ModifyEntityResult<Asset>.Failure(assetPreparationResult.ErrorMessage,
                        WriteResult.FailedValidation)
                };
            }
            
            var updatedAsset = assetPreparationResult.UpdatedAsset!;
            var requiresEngineNotification = assetPreparationResult.RequiresReingest || alwaysReingest;

            if (existingAsset == null)
            {
                var preset = settings.DLCS.IngestDefaults.GetPresets((char)updatedAsset.Family!,
                    updatedAsset.MediaType ?? string.Empty);
                var deliveryChannelChanged = SetDeliveryChannel(updatedAsset, preset);
                if (deliveryChannelChanged)
                {
                    requiresEngineNotification = true;
                }
                
                var imagePolicyChanged = await SelectImageOptimisationPolicy(updatedAsset, preset);
                if (imagePolicyChanged)
                {
                    // NB the AssetPreparer has already inspected image policy, but this will pick up
                    // a change from default.
                    requiresEngineNotification = true;
                }

                var thumbnailPolicyChanged = await SelectThumbnailPolicy(updatedAsset, preset);
                if (thumbnailPolicyChanged)
                {
                    // We won't alter the value of requiresEngineNotification
                    // TODO thumbs will be backfilled.
                    // This could be a config setting.
                }
            }

            if (requiresEngineNotification)
            {
                updatedAsset.SetFieldsForIngestion();

                if (requiresReingestPreSave != null)
                {
                    await requiresReingestPreSave(updatedAsset);
                }
            }
            else
            {
                updatedAsset.MarkAsFinished();
            }

            var assetAfterSave = await assetRepository.Save(updatedAsset, existingAsset != null, cancellationToken);

            // Restore fields that are not persisted but are required
            if (updatedAsset.InitialOrigin.HasText())
            {
                assetAfterSave.InitialOrigin = asset.InitialOrigin;
            }

            return new ProcessAssetResult
            {
                ExistingAsset = existingAsset,
                RequiresEngineNotification = requiresEngineNotification,
                Result = ModifyEntityResult<Asset>.Success(assetAfterSave,
                    existingAsset == null ? WriteResult.Created : WriteResult.Updated)
            };
        }
        catch (Exception e)
        {
            return new ProcessAssetResult
            {
                Result = ModifyEntityResult<Asset>.Failure(e.Message, WriteResult.Error)
            };
        }
    }

    private bool SetDeliveryChannel(Asset updatedAsset, IngestPresets preset)
    {
        // Creation, set DeliveryChannel to default value for Family, if not already set
        if (updatedAsset.DeliveryChannels.IsNullOrEmpty())
        {
            updatedAsset.DeliveryChannels = preset.DeliveryChannel;
        }
        
        // Creation, set image delivery channels to default values for media type, if not already set
        if (updatedAsset.ImageDeliveryChannels.IsNullOrEmpty())
        {
            updatedAsset.ImageDeliveryChannels = new List<ImageDeliveryChannel>();
            
            var matchedDeliveryChannels =
                MatchedDeliveryChannelDictionary(updatedAsset.MediaType!, updatedAsset.Space, updatedAsset.Customer);

            foreach (var deliveryChannel in matchedDeliveryChannels)
            {
                updatedAsset.ImageDeliveryChannels.Add(new ImageDeliveryChannel()
                {
                    ImageId = updatedAsset.Id,
                    DeliveryChannelPolicyId = deliveryChannel.Id,
                    Channel = deliveryChannel.Channel
                });
            }
            return true;
        }

        return false;
    }
    
    private List<DeliveryChannelPolicy> MatchedDeliveryChannelDictionary(string mediaType, int space, int customerId)
    {
        var perChannelWithSpace = AssembleDefaultDeliveryChannelMatchDictionary(customerId, space);

        var completedMatch = new List<DeliveryChannelPolicy>();
        
        foreach (var key in perChannelWithSpace.OrderByDescending(p => p.Key.space))
        {
            var matchedToDefault = MatchedAgainstDictionary(mediaType, key.Value);

            if (matchedToDefault != null)
            {
                if (completedMatch.All(m => m.Channel != key.Key.channel))
                {
                    completedMatch.Add(matchedToDefault);
                }
            }
        }

        return completedMatch;
    }

    private Dictionary<(string channel, int space), List<DefaultDeliveryChannel>> AssembleDefaultDeliveryChannelMatchDictionary(int customerId, int space)
    {
        var perChannelWithSpace = new Dictionary<(string channel, int space), List<DefaultDeliveryChannel>>();

        var defaultDeliveryChannelPoliciesForSpace =
            defaultDeliveryChannelRepository.GetDefaultDeliveryChannelsForCustomer(customerId, space);

        foreach (var defaultDeliveryChannel in defaultDeliveryChannelPoliciesForSpace)
        {
            if (!perChannelWithSpace.ContainsKey((defaultDeliveryChannel.DeliveryChannelPolicy.Channel, defaultDeliveryChannel.Space)))
            {
                perChannelWithSpace.Add((defaultDeliveryChannel.DeliveryChannelPolicy.Channel, defaultDeliveryChannel.Space), 
                    new List<DefaultDeliveryChannel> { defaultDeliveryChannel });
            }
            else
            {
                perChannelWithSpace[(defaultDeliveryChannel.DeliveryChannelPolicy.Channel, defaultDeliveryChannel.Space)]
                    .Add(defaultDeliveryChannel);
            }
        }

        return perChannelWithSpace;
    }


    private DeliveryChannelPolicy? MatchedAgainstDictionary(string mediaType, List<DefaultDeliveryChannel> matchedList)
    {
        var matched = new List<DefaultDeliveryChannel>();

        foreach (var defaultDeliveryChannelToMatch in matchedList)
        {
            if (FileSystemName.MatchesSimpleExpression(defaultDeliveryChannelToMatch.MediaType, mediaType))
            {
                matched.Add(defaultDeliveryChannelToMatch);
            };
        }
        
        return matched.Count == 0 ? null : matched.MaxBy(m => m.MediaType)!.DeliveryChannelPolicy;
    }
    
    private async Task<bool> SelectThumbnailPolicy(Asset asset, IngestPresets ingestPresets)
    {
        bool changed = false; 
        if (MIMEHelper.IsImage(asset.MediaType))
        {
            changed = await SetThumbnailPolicy(ingestPresets.ThumbnailPolicy, asset);
        }

        return changed;
    }

    private async Task<bool> SelectImageOptimisationPolicy(Asset asset, IngestPresets ingestPresets)
    {
        bool changed = await SetImagePolicy(ingestPresets.OptimisationPolicy, asset);;
        
        if (MIMEHelper.IsImage(asset.MediaType) && asset.HasDeliveryChannel(AssetDeliveryChannels.Image))
        {
            changed = await SetImagePolicy(ingestPresets.OptimisationPolicy, asset);
        }
        else if (asset.HasDeliveryChannel(AssetDeliveryChannels.Timebased) &&
                 (MIMEHelper.IsAudio(asset.MediaType) ||
                  MIMEHelper.IsVideo(asset.MediaType)))
        {
            changed = await SetImagePolicy(ingestPresets.OptimisationPolicy, asset);
        }

        return changed;
    }

    private async Task<bool> SetImagePolicy(string? defaultKey, Asset asset)
    {
        string? incomingPolicy = asset.ImageOptimisationPolicy;
        ImageOptimisationPolicy? policy = null;
        if (incomingPolicy.HasText())
        {
            policy = await policyRepository.GetImageOptimisationPolicy(incomingPolicy, asset.Customer);
        }

        if (policy == null && defaultKey.HasText())
        {
            // The asset doesn't have a valid ImageOptimisationPolicy
            // This is adapted from Deliverator, but there wasn't a way of 
            // taking the policy from the incoming PUT. There now is.
            var imagePolicy = await policyRepository.GetImageOptimisationPolicy(defaultKey, asset.Customer);
            if (imagePolicy != null)
            {
                asset.ImageOptimisationPolicy = imagePolicy.Id;
            }
        }

        return asset.ImageOptimisationPolicy != incomingPolicy;
    }
    
    private async Task<bool> SetThumbnailPolicy(string defaultPolicy, Asset asset)
    {
        string? incomingPolicy = asset.ThumbnailPolicy;
        ThumbnailPolicy? policy = null;
        if (incomingPolicy.HasText())
        {
            policy = await policyRepository.GetThumbnailPolicy(incomingPolicy);
        }

        if (policy == null)
        {
            var thumbnailPolicy = await policyRepository.GetThumbnailPolicy(defaultPolicy);
            if (thumbnailPolicy != null)
            {
                asset.ThumbnailPolicy = thumbnailPolicy.Id;
            }
        }

        return asset.ThumbnailPolicy != incomingPolicy;
    }
}

public class ProcessAssetResult
{
    public ModifyEntityResult<Asset> Result { get; set; }
    public Asset? ExistingAsset { get; set; }
    public bool RequiresEngineNotification { get; set; }

    public bool IsSuccess => Result.IsSuccess;
}