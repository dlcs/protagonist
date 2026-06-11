using DLCS.Model.Assets;

namespace DLCS.AWS.SNS;

public class BatchCompletedNotification(Batch completedBatch) : IBatchCompletedNotification
{
    public int Id { get; } = completedBatch.Id;

    public int Customer { get; } = completedBatch.Customer;

    public int Count { get; } = completedBatch.Count;

    public int Completed { get; } = completedBatch.Completed;

    public int Errors { get; } = completedBatch.Errors;
    
    public DateTime Submitted { get; } = completedBatch.Submitted;

    public DateTime? Finished { get; } = completedBatch.Finished;
    
    public bool Superseded { get; private set; } = completedBatch.Superseded;
}
