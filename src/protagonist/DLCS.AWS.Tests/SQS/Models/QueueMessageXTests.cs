using System.Text.Json.Nodes;
using DLCS.AWS.SQS;

namespace DLCS.AWS.Tests.SQS.Models;

public class QueueMessageXTests
{
    [Fact]
    public void GetMessageContents_ReturnsBody_IfSqsOrigin()
    {
        // Arrange
        var body = new JsonObject { ["foo"] = "bar" };
        var queueMessage = new QueueMessage { Body = body };
        
        // Act
        var contents = queueMessage.GetMessageContents();
        
        // Assert
        contents.Should().BeEquivalentTo(body);
    }
    
    [Fact]
    public void GetMessageContents_ReturnsBody_IfSnsOrigin()
    {
        // Arrange
        var expected = new JsonObject { ["foo"] = "bar" }.ToJsonString();

        var body = new JsonObject
        {
            ["Type"] = "Notification",
            ["MessageId"] = "1715cbc2-2aa2-5dc3-b8f6-97faca42118a",
            ["Message"] = expected,
            ["TopicArn"] = "arn:aws:sns:eu-west-1:123456789012:my-topic-name",
            ["Timestamp"] = "2023-01-11T16:06:56.524Z",
            ["SignatureVersion"] = "1",
            ["Signature"] = "123123",
            ["SigningCertURL"] = "https://sns.eu-west-1.amazonaws.com/SimpleNotificationService-123123123.pem",
            ["UnsubscribeUrl"] = "https://sns.eu-west-1.amazonaws.com/?Action=Unsubscribe..."
        };
        var queueMessage = new QueueMessage { Body = body };
        
        // Act
        var contents = queueMessage.GetMessageContents();
        
        // Assert
        contents.ToJsonString().Should().BeEquivalentTo(expected);
    }

    [Fact] public void GetMessageContents_ReturnsNull_IfSnsOrigin_AndBodyNotJson()
    {
        // Arrange
        var body = new JsonObject
        {
            ["Type"] = "Notification",
            ["MessageId"] = "1715cbc2-2aa2-5dc3-b8f6-97faca42118a",
            ["Message"] = "hello-world",
            ["TopicArn"] = "arn:aws:sns:eu-west-1:123456789012:my-topic-name",
            ["Timestamp"] = "2023-01-11T16:06:56.524Z",
            ["SignatureVersion"] = "1",
            ["Signature"] = "123123",
            ["SigningCertURL"] = "https://sns.eu-west-1.amazonaws.com/SimpleNotificationService-123123123.pem",
            ["UnsubscribeUrl"] = "https://sns.eu-west-1.amazonaws.com/?Action=Unsubscribe..."
        };
        var queueMessage = new QueueMessage { Body = body };
        
        // Act
        var contents = queueMessage.GetMessageContents();
        
        // Assert
        contents.Should().BeNull();
    }
}