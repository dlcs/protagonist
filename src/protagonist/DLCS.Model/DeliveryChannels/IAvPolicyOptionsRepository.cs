using System.Collections.Generic;
using System.Threading.Tasks;

namespace DLCS.Model.DeliveryChannels;

public interface IAvPolicyOptionsRepository
{
    public Task<IReadOnlyCollection<string>?> RetrieveAvChannelPolicyOptions();
}