namespace DLCS.AWS.SQS;

public interface IQueueSender
{
    /// <summary>
    /// Queue message to specified queue
    /// </summary>
    /// <param name="queueName">Name of queue to send message to</param>
    /// <param name="messageContents">Serialized contents of message to send</param>
    /// <param name="messageAttributes">Any custom message attributes to include along with the message.</param>
    /// <param name="cancellationToken">Current CancellationToken</param>
    /// <returns>Boolean value indicating success</returns>
    Task<bool> QueueMessage(string queueName, string messageContents, IDictionary<string, string> messageAttributes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queue messages to specified queue.
    /// 
    /// This is more efficient than calling <see cref="QueueMessage(string,string,IDictionary{string,string},System.Threading.CancellationToken)"/> multiple times as it will batch creation of
    /// message to underlying queue API 
    /// </summary>
    /// <param name="queueName">Name of queue to send message to</param>
    /// <param name="messageContents">Serialized contents of messages to send</param>
    /// <param name="batchIdentifier">Unique id for batch</param>
    /// <param name="messageAttributes">Any custom message attributes to include along with the message.</param>
    /// <param name="cancellationToken">Current CancellationToken</param>
    /// <returns>
    /// Count of items successfully sent. Messages batched, a failure sending one batch does not prevent the remainder
    /// being sent. A returned count lower than the <paramref name="messageContents"/> count is a partial send, where
    /// the messages counted are queued and the remainder are dropped. Callers that require all-or-nothing delivery must
    /// reconcile themselves.
    /// </returns>
    Task<int> QueueMessages(string queueName, IReadOnlyCollection<string> messageContents, string batchIdentifier,
        IDictionary<string, string>? messageAttributes, CancellationToken cancellationToken = default);
}
