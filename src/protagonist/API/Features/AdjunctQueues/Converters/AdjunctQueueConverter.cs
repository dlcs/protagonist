using HydraAdjunctQueue = DLCS.HydraModel.CustomerAdjunctQueue;
using EntityAdjunctQueue = DLCS.Model.Processing.AdjunctQueue;

namespace API.Features.AdjunctQueues.Converters;

/// <summary>
/// Conversion between API and EF forms of AdjunctQueue resource
/// </summary>
public static class AdjunctQueueConverter
{
    /// <summary>
    /// Convert AdjunctQueue entity to API resource
    /// </summary>
    public static HydraAdjunctQueue ToHydra(this EntityAdjunctQueue adjunctQueue, string baseUrl)
    {
        var hydra = new HydraAdjunctQueue(baseUrl, adjunctQueue.Customer);
        hydra.Size = adjunctQueue.Size;
        hydra.BatchesWaiting = adjunctQueue.BatchesWaiting;
        hydra.AdjunctsWaiting = adjunctQueue.AdjunctsWaiting;

        return hydra;
    }
}
