namespace API.Features.Image;

public class AssetBeforeProcessing
{
    public AssetBeforeProcessing(DLCS.Model.Assets.Asset asset, DeliveryChannelsBeforeProcessing[] deliveryChannelsBeforeProcessing)
    {
        Asset = asset;
        DeliveryChannelsBeforeProcessing = deliveryChannelsBeforeProcessing;
    }
    
    public DLCS.Model.Assets.Asset Asset { get; init; }

    public DeliveryChannelsBeforeProcessing[] DeliveryChannelsBeforeProcessing { get; init; }
}

public record DeliveryChannelsBeforeProcessing(string? Channel, string? Policy);