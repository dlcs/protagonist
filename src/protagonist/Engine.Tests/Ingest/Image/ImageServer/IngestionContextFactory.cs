using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Policies;
using Engine.Ingest;
using Engine.Ingest.Persistence;

namespace Engine.Tests.Ingest.Image.ImageServer;

public static class IngestionContextFactory
{
    public static IngestionContext GetIngestionContext(string assetId = "/1/2/something",
        string contentType = "image/jpg", CustomerOriginStrategy? cos = null, 
        bool optimised = false, string imageDeliveryChannelPolicy = "default")
    {
        cos ??= new CustomerOriginStrategy { Strategy = OriginStrategyType.Default, Optimised = optimised };
        var asset = new Asset
        {
            Id = AssetId.FromString(assetId), Customer = 1, Space = 2,
            DeliveryChannels = new[] { AssetDeliveryChannels.Image }, MediaType = contentType
        };
        
        asset.ImageDeliveryChannels = new List<ImageDeliveryChannel>()
        {
            new()
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicyId = 1,
                DeliveryChannelPolicy = new DeliveryChannelPolicy()
                {
                    Name = imageDeliveryChannelPolicy
                }
            },
            new()
            {
                Channel = AssetDeliveryChannels.Thumbnails,
                DeliveryChannelPolicyId = 2,
                DeliveryChannelPolicy = new DeliveryChannelPolicy()
                {
                    PolicyData = "[\"1000,1000\",\"400,400\",\"200,200\",\"100,100\"]"
                }
            }
        };

        var context = new IngestionContext(asset);
        var assetFromOrigin = new AssetFromOrigin(asset.Id, 123, "./scratch/here.jpg", contentType)
        {
            CustomerOriginStrategy = cos
        };
        
        return context.WithAssetFromOrigin(assetFromOrigin);
    }
}