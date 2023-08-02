namespace DLCS.AWS.Settings;

/// <summary>
/// Strongly typed AWSSettings object.
/// </summary>
/// <remarks>This is a shared settings object but not all services will required all properties to be set</remarks>
public class AWSSettings
{
    /// <summary>
    /// AWS profile name.
    /// </summary>
    public string Profile { get; set; }
    
    /// <summary>
    /// AWS region.
    /// </summary>
    public string Region { get; set; }
    
    /// <summary>
    /// If true, service will use LocalStack and custom ServiceUrl
    /// </summary>
    public bool UseLocalStack { get; set; } = false;

    /// <summary>
    /// S3 Settings
    /// </summary>
    public S3Settings S3 { get; set; } = new();
    
    /// <summary>
    /// SQS Settings
    /// </summary>
    public SQSSettings SQS { get; set; } = new();

    /// <summary>
    /// SNS Settings
    /// </summary>
    public SNSSettings SNS { get; set; } = new();

    /// <summary>
    /// Cloudfront settings
    /// </summary>
    public CloudfrontSettings CloudfrontSettings { get; set; } = new();
}