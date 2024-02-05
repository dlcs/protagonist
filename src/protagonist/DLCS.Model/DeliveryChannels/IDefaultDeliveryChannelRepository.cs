using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Model.DeliveryChannels;

public interface IDefaultDeliveryChannelRepository
{
    public Task<bool> AddCustomerDefaultDeliveryChannels(int customerId, CancellationToken cancellationToken);
}