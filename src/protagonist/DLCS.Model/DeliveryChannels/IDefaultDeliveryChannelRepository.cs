using System.Collections.Generic;

namespace DLCS.Model.DeliveryChannels;

public interface IDefaultDeliveryChannelRepository
{
    public List<DefaultDeliveryChannel> GetDefaultDeliveryChannelsForCustomer(int customer, int space);
}