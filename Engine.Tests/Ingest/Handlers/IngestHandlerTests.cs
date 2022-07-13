using System.Text.Json.Nodes;
using DLCS.AWS.SQS;
using DLCS.Model.Messaging;
using Engine.Ingest;
using Engine.Ingest.Handlers;
using Engine.Ingest.Models;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Engine.Tests.Ingest.Handlers;

public class IngestHandlerTests
{
    private readonly IAssetIngester assetIngester;
    private readonly IngestHandler sut; 
    
    public IngestHandlerTests()
    {
        assetIngester = A.Fake<IAssetIngester>();
        sut = new IngestHandler(assetIngester, new NullLogger<IngestHandler>());
    }
    
    [Fact]
    public async Task HandleMessage_ReturnsFalse_IfInvalidJsonType_LegacyMessage()
    {
        // Arrange
        var body = new JsonObject
        {
            ["_type"] = "type",
            ["_created"] = "not-a-date"
        };
        var queueMessage = new QueueMessage { Body = body };

        // Act
        var success = await sut.HandleMessage(queueMessage, CancellationToken.None);
        
        // Assert
        A.CallTo(() => assetIngester.Ingest(A<IncomingIngestEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
        success.Should().BeFalse();
    }
    
    [Theory]
    [InlineData(IngestResult.Failed)]
    [InlineData(IngestResult.Unknown)]
    public async Task HandleMessage_ReturnsFalse_IfFailedOrUnknown_LegacyMessage(IngestResult result)
    {
        // Arrange
        var body = new JsonObject
        {
            ["_type"] = "type"
        };
        var queueMessage = new QueueMessage { Body = body };
        A.CallTo(() => assetIngester.Ingest(A<IncomingIngestEvent>._, A<CancellationToken>._)).Returns(result);
        
        // Act
        var success = await sut.HandleMessage(queueMessage, CancellationToken.None);
        
        // Assert
        A.CallTo(() => assetIngester.Ingest(A<IncomingIngestEvent>._, A<CancellationToken>._)).MustHaveHappened();
        success.Should().BeFalse();
    }
    
    [Theory]
    [InlineData(IngestResult.Success)]
    [InlineData(IngestResult.QueuedForProcessing)]
    public async Task HandleMessage_ReturnsFalse_IfSuccessOrQueued_LegacyMessage(IngestResult result)
    {
        // Arrange
        var body = new JsonObject
        {
            ["_type"] = "type"
        };
        var queueMessage = new QueueMessage { Body = body };
        A.CallTo(() => assetIngester.Ingest(A<IncomingIngestEvent>._, A<CancellationToken>._)).Returns(result);
        
        // Act
        var success = await sut.HandleMessage(queueMessage, CancellationToken.None);
        
        // Assert
        A.CallTo(() => assetIngester.Ingest(A<IncomingIngestEvent>._, A<CancellationToken>._)).MustHaveHappened();
        success.Should().BeTrue();
    }
    
    [Fact]
    public async Task HandleMessage_ReturnsFalse_IfInvalidJsonType()
    {
        // Arrange
        var body = new JsonObject
        {
            ["created"] = "not-a-date"
        };
        var queueMessage = new QueueMessage { Body = body };

        // Act
        var success = await sut.HandleMessage(queueMessage, CancellationToken.None);
        
        // Assert
        A.CallTo(() => assetIngester.Ingest(A<IngestAssetRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
        success.Should().BeFalse();
    }
    
    [Theory]
    [InlineData(IngestResult.Failed)]
    [InlineData(IngestResult.Unknown)]
    public async Task HandleMessage_ReturnsFalse_IfFailedOrUnknown(IngestResult result)
    {
        // Arrange
        var body = new JsonObject
        {
            ["created"] = "1985-10-26T09:00:00"
        };
        var queueMessage = new QueueMessage { Body = body };
        A.CallTo(() => assetIngester.Ingest(A<IngestAssetRequest>._, A<CancellationToken>._)).Returns(result);
        
        // Act
        var success = await sut.HandleMessage(queueMessage, CancellationToken.None);
        
        // Assert
        A.CallTo(() => assetIngester.Ingest(A<IngestAssetRequest>._, A<CancellationToken>._)).MustHaveHappened();
        success.Should().BeFalse();
    }
    
    [Theory]
    [InlineData(IngestResult.Success)]
    [InlineData(IngestResult.QueuedForProcessing)]
    public async Task HandleMessage_ReturnsFalse_IfSuccessOrQueued(IngestResult result)
    {
        // Arrange
        var body = new JsonObject
        {
            ["created"] = "1985-10-26T09:00:00"
        };
        var queueMessage = new QueueMessage { Body = body };
        A.CallTo(() => assetIngester.Ingest(A<IngestAssetRequest>._, A<CancellationToken>._)).Returns(result);
        
        // Act
        var success = await sut.HandleMessage(queueMessage, CancellationToken.None);
        
        // Assert
        A.CallTo(() => assetIngester.Ingest(A<IngestAssetRequest>._, A<CancellationToken>._)).MustHaveHappened();
        success.Should().BeTrue();
    }
}