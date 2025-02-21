using HydraCustomerQueue = DLCS.HydraModel.CustomerQueue;
using EntityCustomerQueue = DLCS.Model.Processing.CustomerQueue;

namespace API.Features.Queues.Converters;

/// <summary>
/// Conversion between API and EF forms of CustomerQueue resource
/// </summary>
public static class CustomerQueueConverter
{
    /// <summary>
    /// Convert CustomerQueue entity to API resource
    /// </summary>
    public static HydraCustomerQueue ToHydra(this EntityCustomerQueue customerQueue, string baseUrl)
    {
        var hydra = new HydraCustomerQueue(baseUrl, customerQueue.Customer);
        hydra.Size = customerQueue.Size;
        hydra.BatchesWaiting = customerQueue.BatchesWaiting;
        hydra.ImagesWaiting = customerQueue.ImagesWaiting;

        return hydra;
    }
}