using System.Collections.Generic;
using API.Exceptions;
using DLCS.Core.Exceptions;
using DLCS.Core.Types;

namespace API.Features.Customer.Validation;

internal static class ImageIdListValidation
{
    /// <summary>
    /// Validate that imageIds are all in a valid format and are all for the same customer
    /// </summary>
    public static void ValidateRequest(IReadOnlyCollection<string> assetIdentifiers, int customerId)
    {
        try
        {
            var assetIds = assetIdentifiers.Select(i => AssetId.FromString(i)).ToList();
            
            if (assetIds.Any(a => a.Customer != customerId))
            {
                throw new BadRequestException("Cannot request images for different customer");
            }
        }
        catch (InvalidAssetIdException assetIdEx)
        {
            throw new BadRequestException(assetIdEx.Message, assetIdEx);
        }
    }
}