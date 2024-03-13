using System.Collections.Generic;
using System.Threading.Tasks;

namespace DLCS.Model.DeliveryChannels;

public interface IAvChannelPolicyOptionsRepository
{
    public Task<IReadOnlyCollection<string>?> RetrieveAvChannelPolicyOptions();
}