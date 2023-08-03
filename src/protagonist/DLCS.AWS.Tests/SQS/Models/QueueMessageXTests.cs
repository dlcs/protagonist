using System.Text.Json;
using System.Text.Json.Nodes;
using DLCS.AWS.SQS;
using DLCS.Core.Types;
using DLCS.Model.Assets;

namespace DLCS.AWS.Tests.SQS.Models;

public class QueueMessageXTests
{
    private readonly JsonSerializerOptions settings = new(JsonSerializerDefaults.Web);
    
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

    [Fact] 
    public void GetMessageContents_ReturnsNull_IfSnsOrigin_AndBodyNotJson()
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
    
    [Fact] 
    public void GetMessageContentsWithType_ReturnsObject_WhenCalledWithObject()
    {
        // Arrange
        var asset = new Asset()
        {
            Id = new AssetId(1, 99, "foo")
        };
        
        var serialized =  JsonSerializer.Serialize(asset, settings);
        
        var queueMessage = new QueueMessage
        {
            Body = JsonNode.Parse(serialized)!.AsObject()
        };
        
        // Act
        var contents = queueMessage.GetMessageContents<Asset>();
        
        // Assert
        contents?.Id.Should().NotBeNull();
        contents?.Id.Customer.Should().Be(1);
        contents?.Id.Space.Should().Be(99);
        contents?.Id.Asset.Should().Be("foo");
    }
    
    [Fact] 
    public void GetMessageContentsWithType_ThrowsException_WhenCalledWithObjectThatCannotBeDeserialized()
    {
        // Arrange
        var queueMessage = new QueueMessage
        {
            Body = new JsonObject { ["id"] = "foo" }
        };

        // Act and Assert
        Assert.Throws<JsonException>(() =>queueMessage.GetMessageContents<Asset>());
    }
    
    [Fact] 
    public void GetMessageContentsWithType_DoesNotThrowException_WhenCalledWithDoNotThrowExceptions()
    {
        // Arrange
        var queueMessage = new QueueMessage
        {
            Body = new JsonObject { ["id"] = "foo" }
        };

        // Act
        var exception = Record.Exception(() => queueMessage.GetMessageContents<Asset>(false));
        
        // Assert
        Assert.Null(exception);
    }
}