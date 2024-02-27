using DLCS.HydraModel;

namespace API.Features.Image;

public class AssetBeforeProcessing
{
    public AssetBeforeProcessing(DLCS.Model.Assets.Asset asset, DeliveryChannel[]? deliveryChannels)
    {
        Asset = asset;
        DeliveryChannels = deliveryChannels;
    }
    
    public DLCS.Model.Assets.Asset Asset { get; init; }

    public DeliveryChannel[]? DeliveryChannels { get; init; }
}