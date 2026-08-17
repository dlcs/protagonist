using DLCS.Model.Processing;
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
        if (customerQueue.Name != QueueNames.Default)
        {
            // Update "id" if this is a named queue (e.g. "priority"). Collection links are not queue-specific
            hydra.Id = $"{hydra.Id}/{customerQueue.Name}";
        }
        hydra.Size = customerQueue.Size;
        hydra.BatchesWaiting = customerQueue.BatchesWaiting;
        hydra.ImagesWaiting = customerQueue.ImagesWaiting;

        return hydra;
    }
}
