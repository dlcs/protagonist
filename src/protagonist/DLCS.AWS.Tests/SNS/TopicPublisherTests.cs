using Amazon.SimpleNotificationService;
using DLCS.AWS.Settings;
using DLCS.AWS.SNS;
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
    }
    
    private TopicPublisher GetSut()
    {
        var settings = Options.Create(new AWSSettings()
        {
            SNS = new SNSSettings()
            {
                DeleteNotificationTopicName = "someTopic"
            }
        });

        return new TopicPublisher(snsClient, new NullLogger<TopicPublisher>(), settings);
    }

    [Fact]
    public async Task PublishToTopicAsync_SuccessfullyPublishesToTopic_WhenCalledWithMessage()
    {
        // Arrange
        var message = new
        {
            someValue = "something"
        };
        var sut = GetSut();

        // Act
        var published = await sut.PublishToTopicAsync(JsonConvert.SerializeObject(message));

        // Assert
        published.Should().BeTrue();
    }
    
    [Fact]
    public async Task PublishToTopicAsync_FailsToCreateTopic_WhenTopicSettingIsNull()
    {
        // Arrange
        var settings = Options.Create(new AWSSettings()
        {
            SNS = new SNSSettings()
            {
                DeleteNotificationTopicName = null
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
        var published = await sut.PublishToTopicAsync(JsonConvert.SerializeObject(message), cancellationTokenSource.Token);

        // Assert
        published.Should().BeFalse();
    }
}