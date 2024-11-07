using System.Net;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using DLCS.AWS.Settings;
using DLCS.AWS.SNS;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Tests.SNS;

public class TopicPublisherTests
{
    private readonly IAmazonSimpleNotificationService snsClient;
    private readonly TopicPublisher sut;

    public TopicPublisherTests()
    {
        snsClient = A.Fake<IAmazonSimpleNotificationService>();

        var settings = Options.Create(new AWSSettings
        {
            SNS = new SNSSettings
            {
                AssetModifiedNotificationTopicArn = "arn:aws:sns:us-east-1:000000000000:assetModified",
                CustomerCreatedTopicArn = "arn:aws:sns:us-east-1:000000000000:customerCreated",
            }
        });

        sut = new TopicPublisher(snsClient, settings, new NullLogger<TopicPublisher>());
    }
    
    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_SuccessfullyPublishesSingleMessage_IfSingleItemInBatch()
    {
        // Arrange
        var notification = new AssetModifiedNotification("message", GetAttributes(ChangeType.Delete, false));

        // Act
        await sut.PublishToAssetModifiedTopic(new[] { notification });

        // Assert
        A.CallTo(() =>
            snsClient.PublishAsync(
                A<PublishRequest>.That.Matches(r =>
                    r.Message == "message" && r.MessageAttributes["messageType"].StringValue == "Delete"),
                A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Theory]
    [InlineData(HttpStatusCode.Accepted, true)]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task PublishToAssetModifiedTopicBatch_SingleItemInBatch_ReturnsSuccessDependentOnStatusCode(HttpStatusCode statusCode, bool expected)
    {
        // Arrange
        var notification = new AssetModifiedNotification("message", GetAttributes(ChangeType.Delete, false));
        A.CallTo(() => snsClient.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
            .Returns(new PublishResponse { HttpStatusCode = statusCode });

        // Act
        var result = await sut.PublishToAssetModifiedTopic(new[] { notification });

        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_SuccessfullyPublishesSingleBatch()
    {
        // Arrange
        var notification = new AssetModifiedNotification("message", GetAttributes(ChangeType.Delete, false));
        var notification2 = new AssetModifiedNotification("message", GetAttributes(ChangeType.Delete, false));

        // Act
        await sut.PublishToAssetModifiedTopic(new[] { notification, notification2 });

        // Assert
        A.CallTo(() =>
            snsClient.PublishBatchAsync(
                A<PublishBatchRequest>.That.Matches(b => b.PublishBatchRequestEntries.All(r =>
                                                             r.Message == "message" &&
                                                             r.MessageAttributes["messageType"].StringValue ==
                                                             "Delete") &&
                                                         b.PublishBatchRequestEntries.Count == 2),
                A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_SuccessfullyPublishesMultipleBatches()
    {
        // Arrange
        var notifications = new List<AssetModifiedNotification>(15);
        for (int x = 0; x < 15; x++)
        {
            notifications.Add(new AssetModifiedNotification(x < 10 ? "message" : "next", GetAttributes(ChangeType.Delete, false)));
        } 

        // Act
        await sut.PublishToAssetModifiedTopic(notifications.ToArray());

        // Assert
        A.CallTo(() =>
            snsClient.PublishBatchAsync(
                A<PublishBatchRequest>.That.Matches(b => b.PublishBatchRequestEntries.All(r =>
                                                             r.Message == "message" &&
                                                             r.MessageAttributes["messageType"].StringValue ==
                                                             "Delete") &&
                                                         b.PublishBatchRequestEntries.Count == 10),
                A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() =>
            snsClient.PublishBatchAsync(
                A<PublishBatchRequest>.That.Matches(b => b.PublishBatchRequestEntries.All(r =>
                                                             r.Message == "next" &&
                                                             r.MessageAttributes["messageType"].StringValue ==
                                                             "Delete") &&
                                                         b.PublishBatchRequestEntries.Count == 5),
                A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_ReturnsTrue_IfAllBatchesSucceed()
    {
        // Arrange
        var notifications = new List<AssetModifiedNotification>(15);
        for (int x = 0; x < 15; x++)
        {
            notifications.Add(new AssetModifiedNotification("message", GetAttributes(ChangeType.Delete, false)));
        }
        
        A.CallTo(() => snsClient.PublishBatchAsync(A<PublishBatchRequest>._, A<CancellationToken>._))
            .Returns(new PublishBatchResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        var response = await sut.PublishToAssetModifiedTopic(notifications.ToArray());

        // Assert
        response.Should().BeTrue();
    }
    
    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_ReturnsFalse_IfAnyBatchFails()
    {
        // Arrange
        var notifications = new List<AssetModifiedNotification>(15);
        for (int x = 0; x < 15; x++)
        {
            notifications.Add(new AssetModifiedNotification("message", GetAttributes(ChangeType.Delete, false)));
        }

        A.CallTo(() => snsClient.PublishBatchAsync(A<PublishBatchRequest>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(
                new PublishBatchResponse { HttpStatusCode = HttpStatusCode.InternalServerError },
                new PublishBatchResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        var response = await sut.PublishToAssetModifiedTopic(notifications.ToArray());

        // Assert
        response.Should().BeFalse();
    }
    
    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_SuccessfullyPublishesSingleMessageWithEngineNotified_IfEngineNotifiedTrue()
    {
        // Arrange
        var notification = new AssetModifiedNotification("message", GetAttributes(ChangeType.Update, true));

        // Act
        await sut.PublishToAssetModifiedTopic(new[] { notification });

        // Assert
        A.CallTo(() =>
            snsClient.PublishAsync(
                A<PublishRequest>.That.Matches(r =>
                    r.Message == "message" && r.MessageAttributes["messageType"].StringValue == "Update" && 
                    r.MessageAttributes["engineNotified"].StringValue == "True"),
                A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_SuccessfullyPublishesSingleBatchWithEngineNotified()
    {
        // Arrange
        var notification = new AssetModifiedNotification("message", GetAttributes(ChangeType.Update, true));
        var notification2 = new AssetModifiedNotification("message", GetAttributes(ChangeType.Update, true));

        // Act
        await sut.PublishToAssetModifiedTopic(new[] { notification, notification2 });

        // Assert
        A.CallTo(() =>
            snsClient.PublishBatchAsync(
                A<PublishBatchRequest>.That.Matches(b => b.PublishBatchRequestEntries.All(r =>
                                                             r.Message == "message" &&
                                                             r.MessageAttributes["messageType"].StringValue ==
                                                             "Update"&& 
                                                             r.MessageAttributes["engineNotified"].StringValue == "True") &&
                                                         b.PublishBatchRequestEntries.Count == 2),
                A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task PublishToCustomerCreatedTopic_ReturnsTrue_IfNoArn()
    {
        // Arrange
        var notification = new CustomerCreatedNotification(new Customer());
        var settings = Options.Create(new AWSSettings { SNS = new SNSSettings() });
        var noArnSut = new TopicPublisher(snsClient, settings, new NullLogger<TopicPublisher>());
        
        // Act
        var result = await noArnSut.PublishToCustomerCreatedTopic(notification, CancellationToken.None);
        
        // Assert
        result.Should().BeFalse("Missing Arn should result in failure");
    }
    
    [Fact]
    public async Task PublishToCustomerCreatedTopic_PublishesMessage()
    {
        // Arrange
        var notification = new CustomerCreatedNotification(new Customer { Id = 1, Name = "Test" });
        var expectedMessage = "{\"name\":\"Test\",\"id\":1}";
        
        // Act
        await sut.PublishToCustomerCreatedTopic(notification, CancellationToken.None);
        
        // Assert
        A.CallTo(() => snsClient.PublishAsync(A<PublishRequest>.That.Matches(r => r.Message == expectedMessage),
            A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Theory]
    [InlineData(HttpStatusCode.Accepted, true)]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task PublishToCustomerCreatedTopic_ReturnsSuccessDependentOnStatusCode(HttpStatusCode statusCode, bool expected)
    {
        // Arrange
        var notification = new CustomerCreatedNotification(new Customer { Id = 1, Name = "Test" });
        A.CallTo(() => snsClient.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
            .Returns(new PublishResponse { HttpStatusCode = statusCode });

        // Act
        var result = await sut.PublishToCustomerCreatedTopic(notification, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }
    
    private Dictionary<string, string> GetAttributes(ChangeType changeType, bool engineNotified)
    {
        var attributes = new Dictionary<string, string>()
        {
            { "messageType", changeType.ToString() }
        };
        if (engineNotified)
        {
            attributes.Add("engineNotified", "True");
        }

        return attributes;
    }
}