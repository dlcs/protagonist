using API.Exceptions;
using API.Features.Assets;
using API.Infrastructure.Requests;
using API.Settings;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
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
    private readonly DeliveryChannelProcessor deliveryChannelProcessor;
    private readonly ApiSettings settings;
    
    public AssetProcessor(
        IApiAssetRepository assetRepository,
        IStorageRepository storageRepository,
        DeliveryChannelProcessor deliveryChannelProcessor,
        IOptionsMonitor<ApiSettings> apiSettings)
    {
        this.assetRepository = assetRepository;
        this.storageRepository = storageRepository;
        this.deliveryChannelProcessor = deliveryChannelProcessor;
        settings = apiSettings.CurrentValue;
    }

    /// <summary>
    /// Process an asset - including validation and handling Update or Insert logic and get ready for ingestion
    /// </summary>
    /// <param name="assetBeforeProcessing">Details needed to create assets</param>
    /// <param name="mustExist">If true, then only Update operations are supported</param>
    /// <param name="alwaysReingest">If true, then engine will be notified</param>
    /// <param name="isBatchUpdate">
    /// If true, this operation is part of a batch save. Allows Batch property to be set
    /// </param>
    /// <param name="requiresReingestPreSave">Optional delegate for modifying asset prior to saving</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    public async Task<ProcessAssetResult> Process(AssetBeforeProcessing assetBeforeProcessing, bool mustExist, bool alwaysReingest, bool isBatchUpdate, 
        Func<Asset, Task>? requiresReingestPreSave = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var assetFromDatabase = await assetRepository.GetAsset(assetBeforeProcessing.Asset.Id, true, true);

            if (assetFromDatabase == null)
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

                var counts =
                    await storageRepository.GetStorageMetrics(assetBeforeProcessing.Asset.Customer, cancellationToken);
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

                if (!counts.CanStoreAssetSize(0, 0))
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
            else if (assetBeforeProcessing.DeliveryChannelsBeforeProcessing.IsNullOrEmpty() && alwaysReingest)
            {
                return new ProcessAssetResult
                {
                    Result = ModifyEntityResult<Asset>.Failure(
                        "Delivery channels are required when updating an existing Asset via PUT",
                        WriteResult.BadRequest
                    )
                };
            }
            
            var existingAsset = assetFromDatabase?.Clone();
            var assetPreparationResult =
                AssetPreparer.PrepareAssetForUpsert(assetFromDatabase, assetBeforeProcessing.Asset, false, isBatchUpdate,
                    settings.RestrictedAssetIdCharacters);

            if (!assetPreparationResult.Success)
            {
                return new ProcessAssetResult
                {
                    Result = ModifyEntityResult<Asset>.Failure(assetPreparationResult.ErrorMessage,
                        WriteResult.FailedValidation)
                };
            }
            
            var updatedAsset = assetPreparationResult.UpdatedAsset!; // this is from Database
            var requiresEngineNotification = assetPreparationResult.RequiresReingest || alwaysReingest;

            var deliveryChannelChanged = await deliveryChannelProcessor.ProcessImageDeliveryChannels(assetFromDatabase,
                updatedAsset, assetBeforeProcessing.DeliveryChannelsBeforeProcessing);
            if (deliveryChannelChanged)
            {
                requiresEngineNotification = true;
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

            var assetAfterSave = await assetRepository.Save(updatedAsset, assetFromDatabase != null, cancellationToken);

            return new ProcessAssetResult
            {
                ExistingAsset = existingAsset,
                RequiresEngineNotification = requiresEngineNotification,
                Result = ModifyEntityResult<Asset>.Success(assetAfterSave,
                    assetFromDatabase == null ? WriteResult.Created : WriteResult.Updated)
            };
        }
        catch (APIException apiEx)
        {
            var resultStatus = (apiEx.StatusCode ?? 500) == 400 ? WriteResult.BadRequest : WriteResult.Error;
            return new ProcessAssetResult
            {
                Result = ModifyEntityResult<Asset>.Failure(apiEx.Message, resultStatus)
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
}

public class ProcessAssetResult
{
    public ModifyEntityResult<Asset> Result { get; set; }
    public Asset? ExistingAsset { get; set; }
    public bool RequiresEngineNotification { get; set; }

    public bool IsSuccess => Result.IsSuccess;
}
