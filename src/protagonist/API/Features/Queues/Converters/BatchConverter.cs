namespace API.Features.Queues.Converters;

using HydraBatch = DLCS.HydraModel.Batch;
using EntityBatch = DLCS.Model.Assets.Batch;

/// <summary>
/// Conversion between API and EF forms of Batch resource
/// </summary>
public static class BatchConverter
{
    /// <summary>
    /// Convert Batch entity to API resource
    /// </summary>
    public static HydraBatch ToHydra(this EntityBatch batch, string baseUrl)
    {
        var hydra = new HydraBatch(baseUrl, batch.Id, batch.Customer, batch.Submitted);
        hydra.Completed = batch.Completed;
        hydra.Count = batch.Count;
        hydra.Errors = batch.Errors;
        hydra.Finished = batch.Finished;
        hydra.Superseded = batch.Superseded;

        return hydra;
    }
}