namespace API.Features.Image;

public class AssetBeforeProcessing
{
    public AssetBeforeProcessing(DLCS.Model.Assets.Asset asset, DeliveryChannelBeforeProcessing[] deliveryChannelsBeforeProcessing)
    {
        Asset = asset;
        DeliveryChannelsBeforeProcessing = deliveryChannelsBeforeProcessing;
    }
    
    public DLCS.Model.Assets.Asset Asset { get; init; }

    public DeliveryChannelBeforeProcessing[] DeliveryChannelsBeforeProcessing { get; init; }
}

public record DeliveryChannelBeforeProcessing(string? Channel, string? Policy);