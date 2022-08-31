namespace DLCS.AWS.SQS;

public interface IQueueSender
{
    /// <summary>
    /// Queue message to specified queue
    /// </summary>
    /// <param name="queueName">Name of queue to send message to</param>
    /// <param name="messageContents">Serialized contents of message to send</param>
    /// <param name="cancellationToken">Current CancellationToken</param>
    /// <returns>Boolean value indicating success</returns>
    Task<bool> QueueMessage(string queueName, string messageContents, CancellationToken cancellationToken = default);
}