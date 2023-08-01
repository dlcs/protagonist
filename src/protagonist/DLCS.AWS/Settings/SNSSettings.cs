namespace DLCS.AWS.Settings;

public class SNSSettings
{
    /// <summary>
    /// Name of the SNS topic for notifying that assets have been modified
    /// </summary>
    public string? AssetModifiedNotificationTopicName { get; set; }
    
    /// <summary>
    /// Service root for SNS. Only used if running LocalStack
    /// </summary>
    public string ServiceUrl { get; set; } = "http://localhost:4566/";
}