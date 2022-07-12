namespace Engine.Messaging.Models;

/// <summary>
/// Generic representation of message pulled from queue.
/// </summary>
public class QueueMessage
{
    public string Body { get; set; }

    public Dictionary<string, string> Attributes { get; set; }
}