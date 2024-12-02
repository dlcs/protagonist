using DLCS.Model.Assets;

namespace DLCS.AWS.SNS;

public class BatchCompletedNotification
{
    public int Id { get; private set; }
    
    public int CustomerId { get; private set; }
    
    public int Total { get; private set; }
    
    public int Success { get; private set; }
    
    public int Errors { get; private set; }
    
    public bool Superseded { get; private set; }
    
    public DateTime Started { get; private set; }
    
    public DateTime? Finished { get; private set; }

    public BatchCompletedNotification(Batch completedBatch)
    {
        Id = completedBatch.Id;
        CustomerId = completedBatch.Customer;
        Total = completedBatch.Count;
        Success = completedBatch.Completed;
        Errors = completedBatch.Errors;
        Superseded = completedBatch.Superseded;
        Started = completedBatch.Submitted;
        Finished = completedBatch.Finished;
    }
}