using System.Text.Json.Nodes;
using DLCS.AWS.ElasticTranscoder.Models;
using DLCS.AWS.SQS;
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

    public TranscodeCompleteHandlerTests()
    {
        completion = A.Fake<ITimebasedIngestorCompletion>();
        sut = new TranscodeCompleteHandler(completion, NullLogger<TranscodeCompleteHandler>.Instance);
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
        const string fileName = "ElasticTranscoderNotification.json";
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
    public async Task Handle_PassesDeserialisedObject_ToCompleteIngest()
    {
        // Arrange
        const string fileName = "ElasticTranscoderNotification.json";
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", fileName);

        var queueMessage = new QueueMessage
        {
            Body = JsonObject.Parse(System.IO.File.OpenRead(filePath)).AsObject()
        };
        var cancellationToken = CancellationToken.None;

        // Act
        await sut.HandleMessage(queueMessage, cancellationToken);
            
        // Assert
        A.CallTo(() => completion.CompleteSuccessfulIngest(new AssetId(2, 1, "engine_vid_1"),
                null,
                A<TranscodeResult>.That.Matches(result =>
                    result.InputKey == "2/1/engine_vid_1/9912" &&
                    result.Outputs.Count == 2 &&
                    result.Outputs[0].Key == "random-guid/2/1/engine_vid_1/full/full/max/max/0/default.mp4" &&
                    result.Outputs[1].Key == "random-guid/2/1/engine_vid_1/full/full/max/max/0/default.webm"),
                cancellationToken))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task Handle_PassesDeserialisedObject_AssetInBatch_ToCompleteIngest()
    {
        // Arrange
        const string fileName = "ElasticTranscoderNotificationBatch.json";
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", fileName);

        var queueMessage = new QueueMessage
        {
            Body = JsonObject.Parse(System.IO.File.OpenRead(filePath)).AsObject()
        };
        var cancellationToken = CancellationToken.None;

        // Act
        await sut.HandleMessage(queueMessage, cancellationToken);
            
        // Assert
        A.CallTo(() => completion.CompleteSuccessfulIngest(new AssetId(2, 1, "engine_vid_1"),
                123,
                A<TranscodeResult>.That.Matches(result =>
                    result.InputKey == "2/1/engine_vid_1/9912" &&
                    result.Outputs.Count == 2 &&
                    result.Outputs[0].Key == "random-guid/2/1/engine_vid_1/full/full/max/max/0/default.mp4" &&
                    result.Outputs[1].Key == "random-guid/2/1/engine_vid_1/full/full/max/max/0/default.webm"),
                cancellationToken))
            .MustHaveHappened();
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_AlwaysReturnsTrue(bool success)
    {
        // Arrange
        const string fileName = "ElasticTranscoderNotification.json";
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", fileName);

        var queueMessage = new QueueMessage
        {
            Body = JsonObject.Parse(System.IO.File.OpenRead(filePath)).AsObject()
        };
        var cancellationToken = CancellationToken.None;

        A.CallTo(() =>
            completion.CompleteSuccessfulIngest(new AssetId(2, 1, "engine_vid_1"), null, A<TranscodeResult>._,
                cancellationToken)).Returns(success);

        // Act
        var result = await sut.HandleMessage(queueMessage, cancellationToken);
            
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public async Task HandleMessage_ReturnsTrue_FromErrorMessage()
    {
        // Arrange
        const string fileName = "ElasticTranscoderErrorNotification.json";
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", fileName);

        var queueMessage = new QueueMessage
        {
            Body = JsonObject.Parse(System.IO.File.OpenRead(filePath)).AsObject()
        };
        
        var cancellationToken = CancellationToken.None;
        var result = await sut.HandleMessage(queueMessage, cancellationToken);
            
        // Assert
        result.Should().BeTrue();
    }
}