using System;

namespace DLCS.Model.Assets;

public static class IDeliverableBatchItemX
{
    /// <summary>
    /// Updates the state of a batch item based on the completion status of a deliverable.
    /// If the deliverable has an error, sets the status of the batch item to 'Error' and
    /// assigns the error message. Otherwise, sets the status to 'Completed'.
    /// Also records the completion time as the current UTC time.
    /// </summary>
    /// <param name="batchItem">The batch item to be updated.</param>
    /// <param name="deliverable">The deliverable providing the completion status and error information.</param>
    public static void FinishBatchItem(this IDeliverableBatchItem batchItem, IDeliverable deliverable)
    {
        if (!string.IsNullOrEmpty(deliverable.Error))
        {
            batchItem.Status = BatchStatus.Error;
            batchItem.Error = deliverable.Error;
        }
        else
        {
            batchItem.Status = BatchStatus.Completed;
        }
        batchItem.Finished = DateTime.UtcNow;
    }
}
