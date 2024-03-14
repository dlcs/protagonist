using System.Collections.Generic;
using System.Threading.Tasks;

namespace DLCS.Model.DeliveryChannels;

public interface IAvChannelPolicyOptionsRepository
{
    /// <summary>
    /// Retrieves a list of possible transcode policies for the iiif-av delivery channel
    /// </summary>
    public Task<IReadOnlyCollection<string>?> RetrieveAvChannelPolicyOptions();
}