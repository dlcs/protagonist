namespace DLCS.AWS.SQS;

/// <summary>
/// Marker interface for SQS message handling logic 
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// Handle message, returning success/failure 
    /// </summary>
    Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken = default);
}