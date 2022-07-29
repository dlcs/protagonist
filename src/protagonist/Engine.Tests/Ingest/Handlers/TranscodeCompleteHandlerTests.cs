using System.Text.Json.Nodes;
using DLCS.AWS.SQS;
using DLCS.Core.Types;
using Engine.Ingest.Completion;
using Engine.Ingest.Handlers;
using Engine.Ingest.Timebased;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Engine.Tests.Ingest.Handlers;

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

        var json = await File.ReadAllTextAsync(filePath);
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
            Body = JsonObject.Parse(File.OpenRead(filePath)).AsObject()
        };
        var cancellationToken = CancellationToken.None;

        // Act
        await sut.HandleMessage(queueMessage, cancellationToken);
            
        // Assert
        A.CallTo(() => completion.CompleteSuccessfulIngest(new AssetId(2, 1, "engine_vid_1"),
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
    public async Task Handle_ReturnsResultOfCompleteIngest(bool success)
    {
        // Arrange
        const string fileName = "ElasticTranscoderNotification.json";
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", fileName);

        var queueMessage = new QueueMessage
        {
            Body = JsonObject.Parse(File.OpenRead(filePath)).AsObject()
        };
        var cancellationToken = CancellationToken.None;

        A.CallTo(() =>
            completion.CompleteSuccessfulIngest(new AssetId(2, 1, "engine_vid_1"), A<TranscodeResult>._,
                cancellationToken)).Returns(success);

        // Act
        var result = await sut.HandleMessage(queueMessage, cancellationToken);
            
        // Assert
        result.Should().Be(success);
    }
}