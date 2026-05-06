using DLCS.Model.Assets;

namespace API.Features.Image;

/// <summary>
/// Represents an Asset and DeliveryChannels as provided in to API request, mapped from Hydra model to internal
/// representation.
/// </summary>
public class AssetBeforeProcessing(Asset asset, DeliveryChannelsBeforeProcessing[]? deliveryChannelsBeforeProcessing)
{
    public Asset Asset { get; } = asset;

    public DeliveryChannelsBeforeProcessing[]? DeliveryChannelsBeforeProcessing { get; } = deliveryChannelsBeforeProcessing;
}

/// <summary>
/// Represents DeliveryChannel information as provided in API request - channel and policy only prior to database
/// identifiers etc 
/// </summary>
/// <param name="Channel">Channel (e.g. 'iiif-img', 'file' etc)</param>
/// <param name="Policy">Name of policy (e.g. 'default', 'video-mp4-480p')</param>
public record DeliveryChannelsBeforeProcessing(string Channel, string? Policy);

public static class AssetBeforeProcessingX
{
    /// <summary>
    /// Returns true if the asset's delivery channels are valid for space 0 (only 'none' allowed),
    /// or if the asset is not in space 0.
    /// </summary>
    public static bool IsValidForSpaceZero(this AssetBeforeProcessing assetBeforeProcessing)
        => assetBeforeProcessing.Asset.Space != 0 || assetBeforeProcessing.DeliveryChannelsBeforeProcessing == null ||
           AssetDeliveryChannels.IsNoneOnly(
               assetBeforeProcessing.DeliveryChannelsBeforeProcessing?.Select(dc => dc.Channel));
}
