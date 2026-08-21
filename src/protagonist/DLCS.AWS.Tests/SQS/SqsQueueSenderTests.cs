using System.Net;
using Amazon.SQS;
using Amazon.SQS.Model;
using DLCS.AWS.Settings;
using DLCS.AWS.SQS;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Tests.SQS;

public class SqsQueueSenderTests
{
    private readonly IAmazonSQS sqsClient;

    public SqsQueueSenderTests()
    {
        sqsClient = A.Fake<IAmazonSQS>();
        A.CallTo(() => sqsClient.GetQueueUrlAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string name, CancellationToken _) =>
                new GetQueueUrlResponse { QueueUrl = $"https://sqs.example/{name}" });
    }

    private SqsQueueSender GetSut()
    {
        var utilities = new SqsQueueUtilities(sqsClient, Options.Create(new AWSSettings()),
            new NullLogger<SqsQueueUtilities>());
        return new SqsQueueSender(sqsClient, utilities, new NullLogger<SqsQueueSender>());
    }

    /// <summary>
    /// AWSSDK v4 leaves response collections null, rather than empty, when the service returns no elements - a batch
    /// where nothing failed comes back with a null Failed collection
    /// </summary>
    private static SendMessageBatchResponse Sent(int count) => new()
    {
        HttpStatusCode = HttpStatusCode.OK,
        Failed = null,
        Successful = Enumerable.Range(0, count)
            .Select(i => new SendMessageBatchResultEntry { Id = i.ToString() }).ToList()
    };

    private static List<string> Messages(int count) =>
        Enumerable.Range(0, count).Select(i => $"{{\"id\":{i}}}").ToList();

    [Fact]
    public async Task QueueMessages_ReturnsFullCount_WhenNothingFailed()
    {
        // Arrange
        A.CallTo(() => sqsClient.SendMessageBatchAsync(A<string>._, A<List<SendMessageBatchRequestEntry>>._,
            A<CancellationToken>._)).ReturnsLazily((string _, List<SendMessageBatchRequestEntry> e,
            CancellationToken _) => Sent(e.Count));

        // Act
        var sent = await GetSut().QueueMessages("no-fail", Messages(5), "batch", null);

        // Assert
        sent.Should().Be(5, "a null Failed collection means nothing failed, not that the send failed");
    }

    [Theory]
    [InlineData(10, 1)]
    [InlineData(11, 2)]
    [InlineData(25, 3)]
    [InlineData(100, 10)]
    public async Task QueueMessages_SendsEveryMessage_WhenSpanningMultipleBatches(int messageCount, int expectedCalls)
    {
        // Arrange
        A.CallTo(() => sqsClient.SendMessageBatchAsync(A<string>._, A<List<SendMessageBatchRequestEntry>>._,
            A<CancellationToken>._)).ReturnsLazily((string _, List<SendMessageBatchRequestEntry> e,
            CancellationToken _) => Sent(e.Count));

        // Act
        var sent = await GetSut().QueueMessages($"span-{messageCount}", Messages(messageCount), "batch", null);

        // Assert
        sent.Should().Be(messageCount);
        A.CallTo(() => sqsClient.SendMessageBatchAsync(A<string>._, A<List<SendMessageBatchRequestEntry>>._,
            A<CancellationToken>._)).MustHaveHappened(expectedCalls, Times.Exactly);
    }

    [Fact]
    public async Task QueueMessages_SendsRemainingBatches_WhenOneBatchThrows()
    {
        // Arrange
        var call = 0;
        A.CallTo(() => sqsClient.SendMessageBatchAsync(A<string>._, A<List<SendMessageBatchRequestEntry>>._,
                A<CancellationToken>._))
            .ReturnsLazily((string _, List<SendMessageBatchRequestEntry> e, CancellationToken _) =>
                ++call == 1 ? throw new AmazonSQSException("boom") : Sent(e.Count));

        // Act
        var sent = await GetSut().QueueMessages("one-throws", Messages(25), "batch", null);

        // Assert
        sent.Should().Be(15, "the failed batch of 10 is dropped but the following batches are still sent");
        A.CallTo(() => sqsClient.SendMessageBatchAsync(A<string>._, A<List<SendMessageBatchRequestEntry>>._,
            A<CancellationToken>._)).MustHaveHappened(3, Times.Exactly);
    }

    [Fact]
    public async Task QueueMessages_ReturnsCountOfSuccessfulOnly_WhenSomeEntriesFail()
    {
        // Arrange
        A.CallTo(() => sqsClient.SendMessageBatchAsync(A<string>._, A<List<SendMessageBatchRequestEntry>>._,
            A<CancellationToken>._)).Returns(new SendMessageBatchResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Successful = [new SendMessageBatchResultEntry { Id = "1" }],
            Failed = [new BatchResultErrorEntry { Id = "2", Message = "nope" }]
        });

        // Act
        var sent = await GetSut().QueueMessages("some-fail", Messages(2), "batch", null);

        // Assert
        sent.Should().Be(1);
    }

    [Fact]
    public async Task QueueMessages_ReturnsZero_WhenEveryBatchThrows()
    {
        // Arrange
        A.CallTo(() => sqsClient.SendMessageBatchAsync(A<string>._, A<List<SendMessageBatchRequestEntry>>._,
            A<CancellationToken>._)).Throws(new AmazonSQSException("boom"));

        // Act
        var sent = await GetSut().QueueMessages("all-throw", Messages(25), "batch", null);

        // Assert
        sent.Should().Be(0);
    }
}
