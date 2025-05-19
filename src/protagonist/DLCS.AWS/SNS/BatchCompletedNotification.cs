using DLCS.Model.Assets;

namespace DLCS.AWS.SNS;

public class BatchCompletedNotification(Batch completedBatch)
{
    public int Id { get; private set; } = completedBatch.Id;

    public int Customer { get; private set; } = completedBatch.Customer;

    public int Count { get; private set; } = completedBatch.Count;

    public int Completed { get; private set; } = completedBatch.Completed;

    public int Errors { get; private set; } = completedBatch.Errors;

    public bool Superseded { get; private set; } = completedBatch.Superseded;

    public DateTime Submitted { get; private set; } = completedBatch.Submitted;

    public DateTime? Finished { get; private set; } = completedBatch.Finished;
}
