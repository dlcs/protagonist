using System.Collections.Generic;
using System.IO.Enumeration;
using API.Features.Assets;
using API.Infrastructure.Requests;
using API.Settings;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Core.Settings;
using DLCS.Core.Strings;
using DLCS.HydraModel;
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
    private readonly IApiAssetRepository assetRepository;
    private readonly IStorageRepository storageRepository;
    private readonly IDefaultDeliveryChannelRepository defaultDeliveryChannelRepository;
    private readonly IDeliveryChannelPolicyRepository deliveryChannelPolicyRepository;
    private readonly ApiSettings settings;
    private const string None = "none";
    
    public AssetProcessor(
        IApiAssetRepository assetRepository,
        IStorageRepository storageRepository,
        IDefaultDeliveryChannelRepository defaultDeliveryChannelRepository,
        IDeliveryChannelPolicyRepository deliveryChannelPolicyRepository,
        IOptionsMonitor<ApiSettings> apiSettings)
    {
        this.assetRepository = assetRepository;
        this.storageRepository = storageRepository;
        this.defaultDeliveryChannelRepository = defaultDeliveryChannelRepository;
        this.deliveryChannelPolicyRepository = deliveryChannelPolicyRepository;
        settings = apiSettings.CurrentValue;
    }

    /// <summary>
    /// Process an asset - including validation and handling Update or Insert logic and get ready for ingestion
    /// </summary>
    /// <param name="assetBeforeProcessing">Asset sent to API</param>
    /// <param name="mustExist">If true, then only Update operations are supported</param>
    /// <param name="alwaysReingest">If true, then engine will be notified</param>
    /// <param name="isBatchUpdate">
    /// If true, this operation is part of a batch save. Allows Batch property to be set
    /// </param>
    /// <param name="requiresReingestPreSave">Optional delegate for modifying asset prior to saving</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <param name="isPriorityQueue">Whether the request is for the priority queue or not</param>
    public async Task<ProcessAssetResult> Process(AssetBeforeProcessing assetBeforeProcessing, bool mustExist, bool alwaysReingest, bool isBatchUpdate, 
        Func<Asset, Task>? requiresReingestPreSave = null, 
        CancellationToken cancellationToken = default)
    {
        Asset? existingAsset;
        try
        {
            existingAsset = await assetRepository.GetAsset(assetBeforeProcessing.Asset.Id, noCache: true);
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

                var counts = await storageRepository.GetStorageMetrics(assetBeforeProcessing.Asset.Customer, cancellationToken);
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
                AssetPreparer.PrepareAssetForUpsert(existingAsset, assetBeforeProcessing.Asset, false, isBatchUpdate, settings.RestrictedAssetIdCharacters);

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
                try
                {
                    var deliveryChannelChanged =
                        SetImageDeliveryChannels(updatedAsset, assetBeforeProcessing.DeliveryChannels);
                    if (deliveryChannelChanged)
                    {
                        requiresEngineNotification = true;
                    }
                }
                catch (InvalidOperationException)
                {
                    return new ProcessAssetResult
                    {
                        Result = ModifyEntityResult<Asset>.Failure(
                            "Failed to match delivery channel policy",
                            WriteResult.Error
                        )
                    };
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
                assetAfterSave.InitialOrigin = assetBeforeProcessing.Asset.InitialOrigin;
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

    private bool SetImageDeliveryChannels(Asset updatedAsset, DeliveryChannel[]? deliveryChannels)
    {
        updatedAsset.ImageDeliveryChannels = new List<ImageDeliveryChannel>();
        // Creation, set image delivery channels to default values for media type, if not already set
        if (deliveryChannels == null)
        {
            var matchedDeliveryChannels =
                MatchedDeliveryChannels(updatedAsset.MediaType!, updatedAsset.Space, updatedAsset.Customer);

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

        if (deliveryChannels.Count(d => d.Channel == AssetDeliveryChannels.None) == 1)
        {
            var deliveryChannelPolicy = deliveryChannelPolicyRepository.RetrieveDeliveryChannelPolicy(updatedAsset.Customer,
                AssetDeliveryChannels.None, None);
            
            updatedAsset.ImageDeliveryChannels.Add(new ImageDeliveryChannel()
                {
                    ImageId = updatedAsset.Id,
                    DeliveryChannelPolicyId = deliveryChannelPolicy.Id,
                    Channel = AssetDeliveryChannels.None
                });
            
            return false;
        }

        foreach (var deliveryChannel in deliveryChannels)
        {
            DeliveryChannelPolicy deliveryChannelPolicy = null!;
            
            deliveryChannelPolicy = deliveryChannelPolicyRepository.RetrieveDeliveryChannelPolicy(
                updatedAsset.Customer,
                deliveryChannel.Channel,
                deliveryChannel.Policy);

            updatedAsset.ImageDeliveryChannels.Add(new ImageDeliveryChannel()
            {
                ImageId = updatedAsset.Id,
                DeliveryChannelPolicyId = deliveryChannelPolicy.Id,
                Channel = deliveryChannel.Channel
            });
        }
            
        return true;
    }


    private List<DeliveryChannelPolicy> MatchedDeliveryChannels(string mediaType, int space, int customerId)
    {
        var completedMatch = new List<DeliveryChannelPolicy>();
        
        var defaultDeliveryChannels =
            defaultDeliveryChannelRepository.GetDefaultDeliveryChannelsForCustomer(customerId, space);
        
        foreach (var defaultDeliveryChannel in defaultDeliveryChannels.OrderByDescending(v => v.Space).ThenByDescending(c => c.MediaType.Length))
        {

            if (completedMatch.Any(d => d.Channel == defaultDeliveryChannel.DeliveryChannelPolicy.Channel))
            {
                continue;
            }

            if (FileSystemName.MatchesSimpleExpression(defaultDeliveryChannel.MediaType, mediaType))
            {
                completedMatch.Add(defaultDeliveryChannel.DeliveryChannelPolicy);
            }
        }

        return completedMatch;
    }
}

public class ProcessAssetResult
{
    public ModifyEntityResult<Asset> Result { get; set; }
    public Asset? ExistingAsset { get; set; }
    public bool RequiresEngineNotification { get; set; }

    public bool IsSuccess => Result.IsSuccess;
}