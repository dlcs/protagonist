using DLCS.Model.Assets;

namespace API.Features.AdjunctQueues.Converters;

/// <summary>
/// Conversion between API and EF forms of AdjunctBatch resource
/// </summary>
public static class AdjunctBatchConverter
{
    /// <summary>
    /// Convert AdjunctBatch entity to API resource
    /// </summary>
    public static DLCS.HydraModel.AdjunctBatch ToHydra(this AdjunctBatch batch, string baseUrl)
    {
        var hydraBatch = new DLCS.HydraModel.AdjunctBatch(baseUrl, batch.Customer, batch.Id)
        {
            Submitted = batch.Submitted,
            Count = batch.Count,
            Completed = batch.Completed,
            Errors = batch.Errors,
            Finished = batch.Finished,
        };
        // SetManually link: the route is /current, not /currentAdjuncts
        hydraBatch.CurrentAdjuncts = $"{hydraBatch.Id}/current";
        return hydraBatch;
    }

    /// <summary>
    /// Convert Hydra AdjunctBatch to entity
    /// </summary>
    public static AdjunctBatch ToDlcsModel(this DLCS.HydraModel.AdjunctBatch hydraBatch)
        => new()
        {
            Id = hydraBatch.ModelId,
            Customer = hydraBatch.CustomerId,
            Submitted = hydraBatch.Submitted,
            Count = hydraBatch.Count,
            Completed = hydraBatch.Completed,
            Errors = hydraBatch.Errors,
            Finished = hydraBatch.Finished,
        };
}
