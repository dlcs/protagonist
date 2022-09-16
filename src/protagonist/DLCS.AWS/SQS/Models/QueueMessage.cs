using System.Text.Json.Nodes;

namespace DLCS.AWS.SQS;

/// <summary>
/// Generic representation of message pulled from queue.
/// </summary>
public class QueueMessage
{
    /// <summary>
    /// The full message body property
    /// </summary>
    public JsonObject Body { get; set; }

    /// <summary>
    /// Any attributes associated with message
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; }
        
    /// <summary>
    /// Unique identifier for message
    /// </summary>
    public string MessageId { get; set; }
    
    /// <summary>
    /// The name of the queue that this message was from
    /// </summary>
    public string QueueName { get; set; }
}