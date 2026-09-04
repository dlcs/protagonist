using System.Text.Json.Nodes;
using DLCS.AWS.Configuration;
using DLCS.AWS.SQS;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models;
using DLCS.Core.Types;
using Engine.Ingest.Timebased;
using Engine.Ingest.Timebased.Completion;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Engine.Tests.Ingest.Timebased;

public class TranscodeCompleteHandlerTests
{
    private readonly TranscodeCompleteHandler sut;
    private readonly ITimebasedIngestorCompletion completion;
    private readonly ICustomerAwsContext customerAwsContext;

    public TranscodeCompleteHandlerTests()
    {
        completion = A.Fake<ITimebasedIngestorCompletion>();
        customerAwsContext = new AsyncLocalCustomerAwsContext();
        sut = new TranscodeCompleteHandler(completion, customerAwsContext,
            NullLogger<TranscodeCompleteHandler>.Instance);
    }
    
    [Fact]
    public async Task HandleMessage_ReturnsFalse_IfUnableToDeserializeMessage()
    {
        // Arrange
        var body = new JsonObject
        {
            ["x"] = "y"
        };
        var queueMessage = new QueueMessage { Body = body };
            
        // Act
        var result = await sut.HandleMessage(queueMessage, CancellationToken.None);
            
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task Handle_ReturnsFalse_IfDlcsIdNotFound()
    {
        // Arrange
        const string fileName = "MediaConvertNotification.json";
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", fileName);

        var json = await System.IO.File.ReadAllTextAsync(filePath);
        json = json.Replace("dlcsId", "__");
        var queueMessage = new QueueMessage
        {
            Body = JsonObject.Parse(json).AsObject()
        };

        // Act
        var result = await sut.HandleMessage(queueMessage, CancellationToken.None);
            
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task Handle_PassesJobIdAndAssetId_ToCompleteIngest()
    {
        // Arrange
        const string fileName = "MediaConvertNotification.json";
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", fileName);

        var json = await System.IO.File.ReadAllTextAsync(filePath);
        json = json.Replace("batchId", "__");
        var queueMessage = new QueueMessage
        {
            Body = JsonObject.Parse(json).AsObject()
        };
        var cancellationToken = CancellationToken.None;

        // Act
        await sut.HandleMessage(queueMessage, cancellationToken);

        // Assert
        A.CallTo(() =>
            completion.CompleteSuccessfulIngest(new AssetId(2, 1, "foo"), null, "123456789123-abcd1f",
                cancellationToken))
            .MustHaveHappened();
    }

    [Fact]
    public async Task Handle_PassesDeserialisedObject_AssetInBatch_ToCompleteIngest()
    {
        // Arrange
        const string fileName = "MediaConvertNotification.json";
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", fileName);

        var queueMessage = new QueueMessage
        {
            Body = JsonObject.Parse(System.IO.File.OpenRead(filePath)).AsObject()
        };
        var cancellationToken = CancellationToken.None;

        // Act
        await sut.HandleMessage(queueMessage, cancellationToken);

        // Assert
        A.CallTo(() =>
                completion.CompleteSuccessfulIngest(new AssetId(2, 1, "foo"), 123, "123456789123-abcd1f",
                    cancellationToken))
            .MustHaveHappened();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_AlwaysReturnsTrue_RegardlessOfIngestCompletionResult(bool success)
    {
        // Arrange
        const string fileName = "MediaConvertNotification.json";
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", fileName);

        var queueMessage = new QueueMessage
        {
            Body = JsonObject.Parse(System.IO.File.OpenRead(filePath)).AsObject()
        };
        var cancellationToken = CancellationToken.None;

        A.CallTo(() =>
                completion.CompleteSuccessfulIngest(new AssetId(2, 1, "engine_vid_1"), A<int?>._, A<string>._,
                    cancellationToken))
            .Returns(success);

        // Act
        var result = await sut.HandleMessage(queueMessage, cancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SetsCustomerAwsContext_ForDurationOfCompletion()
    {
        // Arrange
        const string fileName = "MediaConvertNotification.json";
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", fileName);

        var queueMessage = new QueueMessage
        {
            Body = JsonObject.Parse(System.IO.File.OpenRead(filePath)).AsObject()
        };

        int? customerDuringCompletion = null;
        A.CallTo(() => completion.CompleteSuccessfulIngest(A<AssetId>._, A<int?>._, A<string>._, A<CancellationToken>._))
            .Invokes(() => customerDuringCompletion = customerAwsContext.CurrentCustomer)
            .Returns(true);

        // Act
        await sut.HandleMessage(queueMessage, CancellationToken.None);

        // Assert
        customerDuringCompletion.Should().Be(2, "AWS requests are scoped to the customer that owns the asset");
        customerAwsContext.CurrentCustomer.Should().BeNull("customer is cleared once the message is handled");
    }
}
