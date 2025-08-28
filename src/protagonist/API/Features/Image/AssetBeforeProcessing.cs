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
