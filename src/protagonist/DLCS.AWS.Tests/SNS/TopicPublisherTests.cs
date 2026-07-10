using System.Net;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using DLCS.AWS.Settings;
using DLCS.AWS.SNS;
using DLCS.Model.Assets;
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
                AdjunctModifiedNotificationTopicArn = "arn:aws:sns:us-east-1:000000000000:adjunctModified",
                CustomerCreatedTopicArn = "arn:aws:sns:us-east-1:000000000000:customerCreated",
            }
        });

        sut = new TopicPublisher(snsClient, settings, new NullLogger<TopicPublisher>());
    }
    
    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_SuccessfullyPublishesSingleMessage_IfSingleItemInBatch()
    {
        // Arrange
        var notification = new DeliverableModifiedNotification("message", GetAttributes(ChangeType.Delete, false));

        // Act
        await sut.PublishToDeliverableModifiedTopic(new[] { notification }, DeliverableTopicType.Asset);

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
        var notification = new DeliverableModifiedNotification("message", GetAttributes(ChangeType.Delete, false));
        A.CallTo(() => snsClient.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
            .Returns(new PublishResponse { HttpStatusCode = statusCode });

        // Act
        var result = await sut.PublishToDeliverableModifiedTopic(new[] { notification }, DeliverableTopicType.Asset);

        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_SuccessfullyPublishesSingleBatch()
    {
        // Arrange
        var notification = new DeliverableModifiedNotification("message", GetAttributes(ChangeType.Delete, false));
        var notification2 = new DeliverableModifiedNotification("message", GetAttributes(ChangeType.Delete, false));

        // Act
        await sut.PublishToDeliverableModifiedTopic(new[] { notification, notification2 }, DeliverableTopicType.Asset);

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
        const int batchSize = 5;
        const int numberOfMessages = batchSize * 2;
        var notifications = new List<DeliverableModifiedNotification>(numberOfMessages);
        for (int x = 0; x < numberOfMessages; x++)
        {
            notifications.Add(new DeliverableModifiedNotification(x < batchSize ? "message" : "next", GetAttributes(ChangeType.Delete, false)));
        } 

        // Act
        await sut.PublishToDeliverableModifiedTopic(notifications.ToArray(), DeliverableTopicType.Asset);

        // Assert
        A.CallTo(() =>
            snsClient.PublishBatchAsync(
                A<PublishBatchRequest>.That.Matches(b => b.PublishBatchRequestEntries.All(r =>
                                                             r.Message == "message" &&
                                                             r.MessageAttributes["messageType"].StringValue ==
                                                             "Delete") &&
                                                         b.PublishBatchRequestEntries.Count == batchSize),
                A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() =>
            snsClient.PublishBatchAsync(
                A<PublishBatchRequest>.That.Matches(b => b.PublishBatchRequestEntries.All(r =>
                                                             r.Message == "next" &&
                                                             r.MessageAttributes["messageType"].StringValue ==
                                                             "Delete") &&
                                                         b.PublishBatchRequestEntries.Count == batchSize),
                A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_ReturnsTrue_IfAllBatchesSucceed()
    {
        // Arrange
        var notifications = new List<DeliverableModifiedNotification>(15);
        for (int x = 0; x < 15; x++)
        {
            notifications.Add(new DeliverableModifiedNotification("message", GetAttributes(ChangeType.Delete, false)));
        }
        
        A.CallTo(() => snsClient.PublishBatchAsync(A<PublishBatchRequest>._, A<CancellationToken>._))
            .Returns(new PublishBatchResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        var response = await sut.PublishToDeliverableModifiedTopic(notifications.ToArray(), DeliverableTopicType.Asset);

        // Assert
        response.Should().BeTrue();
    }
    
    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_ReturnsFalse_IfAnyBatchFails()
    {
        // Arrange
        var notifications = new List<DeliverableModifiedNotification>(15);
        for (int x = 0; x < 15; x++)
        {
            notifications.Add(new DeliverableModifiedNotification("message", GetAttributes(ChangeType.Delete, false)));
        }

        A.CallTo(() => snsClient.PublishBatchAsync(A<PublishBatchRequest>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(
                new PublishBatchResponse { HttpStatusCode = HttpStatusCode.InternalServerError },
                new PublishBatchResponse { HttpStatusCode = HttpStatusCode.OK });

        // Act
        var response = await sut.PublishToDeliverableModifiedTopic(notifications.ToArray(), DeliverableTopicType.Asset);

        // Assert
        response.Should().BeFalse();
    }
    
    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_SuccessfullyPublishesSingleMessageWithEngineNotified_IfEngineNotifiedTrue()
    {
        // Arrange
        var notification = new DeliverableModifiedNotification("message", GetAttributes(ChangeType.Update, true));

        // Act
        await sut.PublishToDeliverableModifiedTopic(new[] { notification }, DeliverableTopicType.Asset);

        // Assert
        A.CallTo(() =>
            snsClient.PublishAsync(
                A<PublishRequest>.That.Matches(r =>
                    r.Message == "message" &&
                    r.MessageAttributes[ModifiedNotificationAttributes.MessageType].StringValue == "Update" &&
                    r.MessageAttributes[ModifiedNotificationAttributes.EngineNotified].StringValue ==
                    ModifiedNotificationAttributes.EngineNotifiedValue),
                A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_SuccessfullyPublishesSingleBatchWithEngineNotified()
    {
        // Arrange
        var notification = new DeliverableModifiedNotification("message", GetAttributes(ChangeType.Update, true));
        var notification2 = new DeliverableModifiedNotification("message", GetAttributes(ChangeType.Update, true));

        // Act
        await sut.PublishToDeliverableModifiedTopic(new[] { notification, notification2 }, DeliverableTopicType.Asset);

        // Assert
        A.CallTo(() =>
            snsClient.PublishBatchAsync(
                A<PublishBatchRequest>.That.Matches(b => b.PublishBatchRequestEntries.All(r =>
                                                             r.Message == "message" &&
                                                             r.MessageAttributes[ModifiedNotificationAttributes.MessageType].StringValue ==
                                                             "Update"&&
                                                             r.MessageAttributes[ModifiedNotificationAttributes.EngineNotified].StringValue ==
                                                             ModifiedNotificationAttributes.EngineNotifiedValue) &&
                                                         b.PublishBatchRequestEntries.Count == 2),
                A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task PublishToAdjunctModifiedTopicBatch_SuccessfullyPublishesSingleMessage_IfSingleItemInBatch()
    {
        // Arrange
        var notification = new DeliverableModifiedNotification("message", GetAttributes(ChangeType.Delete, false));

        // Act
        await sut.PublishToDeliverableModifiedTopic([notification], DeliverableTopicType.Adjunct);

        // Assert
        A.CallTo(() =>
            snsClient.PublishAsync(
                A<PublishRequest>.That.Matches(r =>
                    r.Message == "message" && r.MessageAttributes["messageType"].StringValue == "Delete" && 
                    r.TopicArn == "arn:aws:sns:us-east-1:000000000000:adjunctModified"),
                A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task PublishToCustomerCreatedTopic_ReturnsFalse_IfNoArn()
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
    
    [Fact]
    public async Task PublishToBatchCompletedTopic_ReturnsFalse_IfNoArn()
    {
        // Arrange
        var notification = new BatchCompletedNotification(new Batch { Id = 1, Customer = 99 });
        var settings = Options.Create(new AWSSettings { SNS = new SNSSettings() });
        var noArnSut = new TopicPublisher(snsClient, settings, new NullLogger<TopicPublisher>());

        // Act
        var result = await noArnSut.PublishToBatchCompletedTopic(notification, CancellationToken.None);

        // Assert
        result.Should().BeFalse("Missing Arn should result in failure");
        A.CallTo(() => snsClient.PublishAsync(A<PublishRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task PublishToBatchCompletedTopic_PublishesToCorrectArn_WithCustomerIdAttribute()
    {
        // Arrange
        const string arn = "arn:aws:sns:us-east-1:000000000000:batchCompleted";
        var batch = new Batch { Id = 1, Customer = 99, Count = 5, Completed = 5, Errors = 0, Submitted = DateTime.UtcNow };
        var notification = new BatchCompletedNotification(batch);
        var settings = Options.Create(new AWSSettings
        {
            SNS = new SNSSettings { BatchCompletedTopicArn = arn }
        });
        var arnSut = new TopicPublisher(snsClient, settings, new NullLogger<TopicPublisher>());

        // Act
        await arnSut.PublishToBatchCompletedTopic(notification, CancellationToken.None);

        // Assert
        A.CallTo(() => snsClient.PublishAsync(
            A<PublishRequest>.That.Matches(r =>
                r.TopicArn == arn &&
                r.MessageAttributes["CustomerId"].StringValue == "99" &&
                r.MessageAttributes["Type"].StringValue == "Batch"),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
    
    [Fact]
    public async Task PublishToBatchCompletedTopic_Correctly_Serialises()
    {
        // Arrange
        const string arn = "arn:aws:sns:us-east-1:000000000000:batchCompleted";
        var batch = new Batch
        {
            Id = 1, Customer = 99, Count = 5, Completed = 5, Errors = 0, Submitted = DateTime.UtcNow, Superseded = true
        };
        var notification = new BatchCompletedNotification(batch);
        var settings = Options.Create(new AWSSettings
        {
            SNS = new SNSSettings { BatchCompletedTopicArn = arn }
        });
        var arnSut = new TopicPublisher(snsClient, settings, new NullLogger<TopicPublisher>());

        // Act
        await arnSut.PublishToBatchCompletedTopic(notification, CancellationToken.None);

        // Check that 'superseded' is correctly serialised to the message, this is on concrete type only, not interface
        // so verifies that it's not only the interface props that are being serialised. 
        A.CallTo(() => snsClient.PublishAsync(
            A<PublishRequest>.That.Matches(r =>
                r.TopicArn == arn &&
                r.MessageAttributes["CustomerId"].StringValue == "99" &&
                r.Message.Contains("superseded")),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted, true)]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task PublishToBatchCompletedTopic_ReturnsSuccessDependentOnStatusCode(HttpStatusCode statusCode, bool expected)
    {
        // Arrange
        var notification = new BatchCompletedNotification(new Batch { Id = 1, Customer = 99 });
        var settings = Options.Create(new AWSSettings
        {
            SNS = new SNSSettings { BatchCompletedTopicArn = "arn:aws:sns:us-east-1:000000000000:batchCompleted" }
        });
        var arnSut = new TopicPublisher(snsClient, settings, new NullLogger<TopicPublisher>());
        A.CallTo(() => snsClient.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
            .Returns(new PublishResponse { HttpStatusCode = statusCode });

        // Act
        var result = await arnSut.PublishToBatchCompletedTopic(notification, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task PublishToAdjunctBatchCompletedTopic_ReturnsFalse_IfNoArn()
    {
        // Arrange
        var notification = new AdjunctBatchCompletedNotification(new AdjunctBatch { Id = 1, Customer = 99 });
        var settings = Options.Create(new AWSSettings { SNS = new SNSSettings() });
        var noArnSut = new TopicPublisher(snsClient, settings, new NullLogger<TopicPublisher>());

        // Act
        var result = await noArnSut.PublishToAdjunctBatchCompletedTopic(notification, CancellationToken.None);

        // Assert
        result.Should().BeFalse("Missing Arn should result in failure");
        A.CallTo(() => snsClient.PublishAsync(A<PublishRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task PublishToAdjunctBatchCompletedTopic_PublishesToCorrectArn_WithCustomerIdAttribute()
    {
        // Arrange
        const string arn = "arn:aws:sns:us-east-1:000000000000:adjunctBatchCompleted";
        var batch = new AdjunctBatch { Id = 2, Customer = 99, Count = 3, Completed = 3, Errors = 0, Submitted = DateTime.UtcNow };
        var notification = new AdjunctBatchCompletedNotification(batch);
        var settings = Options.Create(new AWSSettings
        {
            SNS = new SNSSettings { AdjunctBatchCompletedTopicArn = arn }
        });
        var arnSut = new TopicPublisher(snsClient, settings, new NullLogger<TopicPublisher>());

        // Act
        await arnSut.PublishToAdjunctBatchCompletedTopic(notification, CancellationToken.None);

        // Assert
        A.CallTo(() => snsClient.PublishAsync(
            A<PublishRequest>.That.Matches(r =>
                r.TopicArn == arn &&
                r.MessageAttributes["CustomerId"].StringValue == "99" &&
                r.MessageAttributes["Type"].StringValue == "AdjunctBatch"),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted, true)]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task PublishToAdjunctBatchCompletedTopic_ReturnsSuccessDependentOnStatusCode(HttpStatusCode statusCode, bool expected)
    {
        // Arrange
        var notification = new AdjunctBatchCompletedNotification(new AdjunctBatch { Id = 1, Customer = 99 });
        var settings = Options.Create(new AWSSettings
        {
            SNS = new SNSSettings { AdjunctBatchCompletedTopicArn = "arn:aws:sns:us-east-1:000000000000:adjunctBatchCompleted" }
        });
        var arnSut = new TopicPublisher(snsClient, settings, new NullLogger<TopicPublisher>());
        A.CallTo(() => snsClient.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
            .Returns(new PublishResponse { HttpStatusCode = statusCode });

        // Act
        var result = await arnSut.PublishToAdjunctBatchCompletedTopic(notification, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task PublishToAssetModifiedTopicBatch_FailsToPublishTopic_WhenArnNotParsed()
    {
        // Arrange
        var notification = new DeliverableModifiedNotification("message", GetAttributes(ChangeType.Delete, false));
        var notification2 = new DeliverableModifiedNotification("message", GetAttributes(ChangeType.Delete, false));
        
        var settings = Options.Create(new AWSSettings
        {
            SNS = new SNSSettings
            {
                AssetModifiedNotificationTopicArn = "notValid"
            }
        });

        var topicPublisher = new TopicPublisher(snsClient, settings, new NullLogger<TopicPublisher>());

        // Act
        var output = await topicPublisher.PublishToDeliverableModifiedTopic(new[] { notification, notification2 }, DeliverableTopicType.Asset);

        // Assert
        output.Should().BeFalse();
    }
    
    private Dictionary<string, string> GetAttributes(ChangeType changeType, bool engineNotified)
    {
        var attributes = new Dictionary<string, string>()
        {
            { ModifiedNotificationAttributes.MessageType, changeType.ToString() }
        };
        if (engineNotified)
        {
            attributes.Add(ModifiedNotificationAttributes.EngineNotified,
                ModifiedNotificationAttributes.EngineNotifiedValue);
        }

        return attributes;
    }
}
