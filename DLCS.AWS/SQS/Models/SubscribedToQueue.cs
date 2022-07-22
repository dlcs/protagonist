using System.Diagnostics;

namespace DLCS.AWS.SQS.Models;

/// <summary>
/// <summary>
/// Model representing a queue that has been subscribed to.
/// </summary>
/// </summary>
/// <param name="Name">Name of queue</param>
/// <param name="MessageType">Type of message handled by queue</param>
/// <param name="Url">Url of queue</param>
/// <typeparam name="TMessageType"></typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record SubscribedToQueue<TMessageType>(string Name, TMessageType MessageType, string Url)
    where TMessageType : Enum
{
    private string DebuggerDisplay => $"{Name} - {Url}";
}