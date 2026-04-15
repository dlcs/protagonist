namespace DLCS.AWS.SNS;

/// <summary>
/// Common properties for BatchCompleted notifications
/// </summary>
public interface IBatchCompletedNotification
{
    int Id { get; }
    int Customer { get; }
    int Count { get; }
    int Completed { get; }
    int Errors { get; }
    DateTime Submitted { get; }
    DateTime? Finished { get; }
}
