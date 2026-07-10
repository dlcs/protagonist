namespace DLCS.AWS.SNS;

/// <summary>
/// Message attribute keys set on deliverable-modified notifications published to SNS.
/// </summary>
public static class ModifiedNotificationAttributes
{
    /// <summary>
    /// The type of the modification - Create, Update or Delete.
    /// </summary>
    public const string MessageType = "messageType";

    /// <summary>
    /// Attribute key used to indicate that the engine has been notified of the modification.
    /// </summary>
    /// <remarks>
    /// Set to "True" only when the engine was notified, and omitted entirely otherwise. Consumers test for presence of
    /// the key, not its value.
    /// </remarks>
    public const string EngineNotified = "engineNotified";

    /// <summary>
    /// Value used for <see cref="EngineNotified"/> when set.
    /// </summary>
    public const string EngineNotifiedValue = "True";
}
