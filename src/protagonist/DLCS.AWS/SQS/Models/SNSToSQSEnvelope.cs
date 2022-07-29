namespace DLCS.AWS.SQS.Models;

/// <summary>
/// Represents an SNS->SQS notification. The message body is contained in the <see cref="Message"/> property
/// </summary>
/// <remarks>
/// See https://docs.aws.amazon.com/sns/latest/dg/sns-large-payload-raw-message-delivery.html#raw-message-examples
/// </remarks>
public class SNSToSQSEnvelope
{
    public string Type { get; set; }
    public string MessageId { get; set; }
    public string TopicArn { get; set; }
    public string Subject { get; set; }
    
    /// <summary>
    /// The raw message body
    /// </summary>
    public string Message { get; set; }
    public string Timestamp { get; set; }
    public string SignatureVersion { get; set; }
    public string Signature { get; set; }
    public string SigningCertURL { get; set; }
    public string UnsubscribeURL { get; set; }
}