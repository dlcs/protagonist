using API.Features.Image;
using API.Features.Image.Requests;
using API.Features.Queues.Requests;
using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets;
using MediatR;

namespace API.Features.DeliveryChannels.Helpers;

public static class SpaceZeroValidator
{
    public static ModifyEntityResult<T>? Validate<T>(IRequest<ModifyEntityResult<T>> request) where T : class
    {
        var failedValidation = false;
        
        switch (typeof(T))
        {
            case
                var cls when cls == typeof(Asset):
            {
                var converted = (CreateOrUpdateImage)request;
                if (!converted.AssetBeforeProcessing!.IsValidForSpaceZero()) failedValidation = true;
                break;
            }
            case
                var cls when cls == typeof(Batch):
            {
                var converted = (CreateBatchOfImages)request;
                var hasInvalidSpaceZeroAsset = converted.AssetsBeforeProcessing.Any(a => !a.IsValidForSpaceZero());
                if (hasInvalidSpaceZeroAsset) failedValidation = true;
                break;
            } 
        }

        if (failedValidation)
        {
            return ModifyEntityResult<T>.Failure(
                "Assets in space 0 can only use the 'none' delivery channel",
                WriteResult.FailedValidation);
        }

        return null;
    }
}
