using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Policies;
using Engine.Ingest;
using Engine.Ingest.Persistence;

namespace Engine.Tests.Ingest.Image.ImageServer;

public static class IngestionContextFactory
{
    public static IngestionContext GetIngestionContext(AssetId assetId,
        string contentType = "image/jpg", CustomerOriginStrategy? cos = null, 
        bool optimised = false, string imageDeliveryChannelPolicy = "default")
    {
        cos ??= new CustomerOriginStrategy { Strategy = OriginStrategyType.Default, Optimised = optimised };
        var asset = new Asset
        {
            Id = assetId, Customer = assetId.Customer, Space = assetId.Space, 
            MediaType = contentType,
            ImageDeliveryChannels = new List<ImageDeliveryChannel>
            {
                new()
                {
                    Channel = AssetDeliveryChannels.Thumbnails,
                    DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ThumbsDefault,
                    DeliveryChannelPolicy = new DeliveryChannelPolicy
                    {
                        PolicyData = "[\"!1000,1000\",\"!400,400\",\"!200,200\",\"!100,100\"]"
                    }
                }
            }
        };

        if (!string.IsNullOrEmpty(imageDeliveryChannelPolicy))
        {
            asset.DeliveryChannels = asset.DeliveryChannels.Concat([AssetDeliveryChannels.Image]).ToArray();
            asset.ImageDeliveryChannels.Add(new ImageDeliveryChannel
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault,
                DeliveryChannelPolicy = new DeliveryChannelPolicy
                {
                    Name = imageDeliveryChannelPolicy
                }
            });
        }

        var context = new IngestionContext(asset);
        var assetFromOrigin = new AssetFromOrigin(asset.Id, 123, "./scratch/here.jpg", contentType)
        {
            CustomerOriginStrategy = cos
        };
        
        return context.WithAssetFromOrigin(assetFromOrigin);
    }
}
