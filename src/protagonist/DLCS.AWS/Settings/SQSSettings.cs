namespace DLCS.AWS.Settings;

/// <summary>
/// Strongly typed SQS settings object
/// </summary>
public class SQSSettings
{
    /// <summary>
    /// Name of queue for ingesting images
    /// </summary>
    public string? ImageQueueName { get; set; }
        
    /// <summary>
    /// Name of priority queue for ingesting images.
    /// </summary>
    /// <remarks>
    /// Behaviour is exactly the same for priority + non-priority but priority will process quicker as there will
    /// be less items in the queue
    /// </remarks>
    public string? PriorityImageQueueName { get; set; }
        
    /// <summary>
    /// Name of queue for ingesting timebased assets
    /// </summary>
    public string? TimebasedQueueName { get; set; }
    
    /// <summary>
    /// Name of queue for ingesting file assets
    /// </summary>
    public string? FileQueueName { get; set; }
    
    /// <summary>
    /// Name of queue for handling callbacks when Timebased assets have been transcoded
    /// </summary>
    public string? TranscodeCompleteQueueName { get; set; }
    
    /// <summary>
    /// Name of queue for handling notifications that assets have been deleted
    /// </summary>
    public string? DeleteNotificationQueueName { get; set; }
    
    /// <summary>
    /// Name of queue for handling notifications that assets have been updated
    /// </summary>
    public string? UpdateNotificationQueueName { get; set; }
    
    /// <summary>
    /// The duration (in seconds) for which the call waits for a message to arrive in the queue before returning
    /// </summary>
    public int WaitTimeSecs { get; set; } = 20;

    /// <summary>
    /// The maximum number of messages to fetch from SQS in single request (valid 1-10)
    /// </summary>
    public int MaxNumberOfMessages { get; set; } = 10;

    /// <summary>
    /// Service root for SQS. Only used if running LocalStack
    /// </summary>
    public string ServiceUrl { get; set; } = "http://localhost:4566/";
    
}