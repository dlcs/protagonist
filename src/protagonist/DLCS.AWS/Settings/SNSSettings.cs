namespace DLCS.AWS.Settings;

public class SNSSettings
{
    /// <summary>
    /// Name of the SNS topic for notifying that assets have been modified
    /// </summary>
    public string? AssetModifiedNotificationTopicArn { get; set; }
    
    /// <summary>
    /// Name of the SNS topic for notifying that customers have been created
    /// </summary>
    public string? CustomerCreatedTopicArn { get; set; }
    
    /// <summary>
    /// Name of the SNS topic for notifying that 
    /// </summary>
    public string? BatchCompletedTopicArn { get; set; }
    
    /// <summary>
    /// Service root for SNS. Only used if running LocalStack
    /// </summary>
    public string ServiceUrl { get; set; } = "http://localhost:4566/";
}