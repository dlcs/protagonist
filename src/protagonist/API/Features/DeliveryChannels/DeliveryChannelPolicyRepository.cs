using DLCS.Core.Caching;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.Policies;
using DLCS.Repository;
using LazyCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Features.DeliveryChannels;

public class DeliveryChannelPolicyRepository : IDeliveryChannelPolicyRepository
{
    private readonly DlcsContext dlcsContext;

    public DeliveryChannelPolicyRepository(
        DlcsContext dlcsContext)
    {
        this.dlcsContext = dlcsContext;
    }

    public DeliveryChannelPolicy RetrieveDeliveryChannelPolicy(int customer, string channel, string policy)
    {
        return dlcsContext.DeliveryChannelPolicies.SingleOrDefault(p =>
                                        p.Customer == customer &&
                                        p.System == false &&
                                        p.Channel == channel &&
                                        p.Name == policy!
                                            .Split('/', StringSplitOptions.None).Last()) ??
                                    dlcsContext.DeliveryChannelPolicies.Single(p =>
                                        p.Customer == 1 &&
                                        p.System == true &&
                                        p.Channel == channel &&
                                        p.Name == policy);
    }
}