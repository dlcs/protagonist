using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using DLCS.AWS.Settings;
using DLCS.AWS.SNS;
using DLCS.Model.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Test.Helpers.Integration;

namespace DLCS.AWS.Tests.SNS;

[Collection(LocalStackCollection.CollectionName)]
[Trait("Category", "Manual")]
public class TopicPublisherTests
{
    private readonly IAmazonSimpleNotificationService snsClient;

    public TopicPublisherTests(LocalStackFixture localStackFixture)
    {
        snsClient = localStackFixture.AWSSNSFactory();
        
        snsClient.CreateTopicAsync(new CreateTopicRequest("someTopic"));
    }
    
    private TopicPublisher GetSut()
    {
        var settings = Options.Create(new AWSSettings()
        {
            SNS = new SNSSettings()
            {
                AssetModifiedNotificationTopicArn = "arn:aws:sns:us-east-1:000000000000:someTopic"
            }
        });
        
        return new TopicPublisher(snsClient, new NullLogger<TopicPublisher>(), settings);
    }

    [Fact]
    public async Task PublishToTopic_SuccessfullyPublishesToTopic_WhenCalledWithMessage()
    {
        // Arrange
        var message = new
        {
            someValue = "something"
        };
        var sut = GetSut();

        // Act
        var published = await sut.PublishToAssetModifiedTopic(JsonConvert.SerializeObject(message), ChangeType.Delete);

        // Assert
        published.Should().BeTrue();
    }
    
    [Fact]
    public async Task PublishToTopic_FailsToRetrieveTopic_WhenTopicSettingIsNull()
    {
        // Arrange
        var settings = Options.Create(new AWSSettings()
        {
            SNS = new SNSSettings()
            {
                AssetModifiedNotificationTopicArn = null
            }
        });

        var sut = new TopicPublisher(snsClient, new NullLogger<TopicPublisher>(), settings);
        
        var message = new
        {
            someValue = "something"
        };

        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(100);
        // Act
        var published = await sut.PublishToAssetModifiedTopic(JsonConvert.SerializeObject(message), ChangeType.Delete, cancellationTokenSource.Token);

        // Assert
        published.Should().BeFalse();
    }
    
    [Fact]
    public async Task PublishToTopic_FailsToRetrieveTopic_WhenTopicSettingIsNotAValidTopic()
    {
        // Arrange
        var settings = Options.Create(new AWSSettings()
        {
            SNS = new SNSSettings()
            {
                AssetModifiedNotificationTopicArn = "arn:aws:sns:us-east-1:000000000000:invalidTopic"
            }
        });

        var sut = new TopicPublisher(snsClient, new NullLogger<TopicPublisher>(), settings);
        
        var message = new
        {
            someValue = "something"
        };
        
        // Act
        var published = await sut.PublishToAssetModifiedTopic(JsonConvert.SerializeObject(message), ChangeType.Delete);

        // Assert
        published.Should().BeFalse();
    }
}