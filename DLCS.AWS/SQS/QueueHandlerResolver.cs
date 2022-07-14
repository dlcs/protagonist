namespace DLCS.AWS.SQS;

/// <summary>
/// Delegate for getting <see cref="IMessageHandler"/> for message of specified type.
/// </summary>
/// <param name="messageType">Type of message to be handled.</param>
public delegate IMessageHandler QueueHandlerResolver<in TMessageType>(TMessageType messageType)
    where TMessageType : Enum;