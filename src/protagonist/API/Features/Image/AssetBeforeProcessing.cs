using DLCS.Model.Assets;

namespace API.Features.Image;

public class AssetBeforeProcessing
{
    public AssetBeforeProcessing(Asset asset, DeliveryChannelsBeforeProcessing[] deliveryChannelsBeforeProcessing)
    {
        Asset = asset;
        DeliveryChannelsBeforeProcessing = deliveryChannelsBeforeProcessing;
    }

    public Asset Asset { get; }

    public DeliveryChannelsBeforeProcessing[] DeliveryChannelsBeforeProcessing { get; }
}

/// <summary>
/// Represents DeliveryChannel information as provided in API request - channel and policy only prior to database
/// identifiers etc 
/// </summary>
/// <param name="Channel">Channel (e.g. 'iiif-img', 'file' etc)</param>
/// <param name="Policy">Name of policy (e.g. 'default', 'video-mp4-480p')</param>
public record DeliveryChannelsBeforeProcessing(string Channel, string? Policy);