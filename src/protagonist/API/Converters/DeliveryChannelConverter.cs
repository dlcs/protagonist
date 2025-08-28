using API.Features.Image;
using DLCS.HydraModel;

namespace API.Converters;

public static class DeliveryChannelConverter
{
    /// <summary>
    /// Convert Hydra model to interim <see cref="DeliveryChannelsBeforeProcessing"/> model
    /// </summary>
    public static DeliveryChannelsBeforeProcessing[]? ToInterimModel(this DeliveryChannel[]? deliveryChannels)
        => deliveryChannels?.Select(d => new DeliveryChannelsBeforeProcessing(d.Channel, d.Policy)).ToArray();
}
