using Amazon.SQS;
using DLCS.AWS.Settings;
using DLCS.AWS.SQS;
using DLCS.AWS.SQS.Models;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;

namespace DLCS.AWS.Tests.SQS;

[Collection(LocalStackCollection.CollectionName)]
[Trait("Category", "Manual")]
public class SqsListenerTests : IAsyncLifetime
{
    private readonly FakeMessageHandler messageHandler;
    private readonly IAmazonSQS sqsClient;
    private string? queueUrl;
    private readonly IServiceScopeFactory scopeFactory;

    public SqsListenerTests(LocalStackFixture localStackFixture)
    {
        sqsClient = localStackFixture.AWSSQSClientFactory();
        messageHandler = new FakeMessageHandler();
        
        scopeFactory = A.Fake<IServiceScopeFactory>();
        var scope = A.Fake<IServiceScope>();
        var serviceProvider = A.Fake<IServiceProvider>();
        A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);
        A.CallTo(() => scope.ServiceProvider).Returns(serviceProvider);
        QueueHandlerResolver<MessageType> handlerResolver = _ => messageHandler;
        A.CallTo(() => serviceProvider.GetService(typeof(QueueHandlerResolver<MessageType>)))
            .Returns(handlerResolver);
    }
    
    public async Task InitializeAsync()
    {
        queueUrl = (await sqsClient.GetQueueUrlAsync(LocalStackFixture.ImageQueueName)).QueueUrl;
        
        async Task SendMessage(string id, int failCount)
        {
            var messageBody = $"{{\"message\":\"{id}\", \"failCount\": {failCount}}}";
            await sqsClient.SendMessageAsync(queueUrl, messageBody);
        }
        
        await SendMessage("failMany", 3);
        await SendMessage("success", 0);
        await SendMessage("failOnce", 1);
    }

    public Task DisposeAsync()
    {
        sqsClient.Dispose();
        return Task.CompletedTask;
    }

    private SqsListener<MessageType> GetSut()
    {
        var subscribedQueue =
            new SubscribedToQueue<MessageType>(LocalStackFixture.ImageQueueName, MessageType.Test, queueUrl);

        var settings = Options.Create(new AWSSettings());

        return new SqsListener<MessageType>(sqsClient, scopeFactory, settings, subscribedQueue, 
            new NullLogger<SqsListener<MessageType>>());
    }

    [Fact]
    public async Task Listen_CallsHandlerForMessagesInQueue_RemovesMessagesFromQueueOnceSuccessfullyHandled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var expected = new Dictionary<string, int>
        {
            ["failMany"] = 4,
            ["success"] = 1,
            ["failOnce"] = 2,
        };
        var sut = GetSut(); 

        // Act
        sut.Listen(cts.Token);

        int count = 0;
        while (count < 10 || !messageHandler.IsComplete)
        {
            count++;
            await Task.Delay(TimeSpan.FromMilliseconds(200), cts.Token);
        }

        // Assert
        sut.IsListening.Should().BeTrue();
        messageHandler.Received.Should().BeEquivalentTo(expected);

        var messageCount = await sqsClient.GetQueueAttributesAsync(queueUrl,
            new List<string>
            {
                "ApproximateNumberOfMessages", "ApproximateNumberOfMessagesDelayed",
                "ApproximateNumberOfMessagesNotVisible"
            });
        messageCount.ApproximateNumberOfMessages.Should().Be(0);
        messageCount.ApproximateNumberOfMessagesNotVisible.Should().Be(0);
        messageCount.ApproximateNumberOfMessagesDelayed.Should().Be(0);
    }
}

public class FakeMessageHandler : IMessageHandler
{
    public readonly Dictionary<string, int> Received = new();

    private int completeCount = 0;

    public bool IsComplete => completeCount == 3;
    
    public Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken = default)
    {
        var uniqueId = message.Body["message"]!.ToString();
        var failCount = int.Parse(message.Body["failCount"]!.ToString());

        Received.TryGetValue(uniqueId, out var receiveCount);
        Received[uniqueId] = receiveCount + 1;

        if (receiveCount == failCount)
        {
            completeCount += 1;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}

public enum MessageType
{
    Test
}