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
        if (customerQueue.Name != "default")
        {
            // A named queue (e.g. priority) is its own resource with its own counts. Its collection
            // links deliberately remain those of the main queue: batches submitted to a named queue
            // appear in the shared batches/active/recent collections, and no per-name sub-routes exist.
            hydra.Id = $"{hydra.Id}/{customerQueue.Name}";
        }
        hydra.Size = customerQueue.Size;
        hydra.BatchesWaiting = customerQueue.BatchesWaiting;
        hydra.ImagesWaiting = customerQueue.ImagesWaiting;

        return hydra;
    }
}