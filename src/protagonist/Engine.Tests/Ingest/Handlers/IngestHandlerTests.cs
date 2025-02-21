using System.Text.Json.Nodes;
using DLCS.AWS.SQS;
using DLCS.Core.Types;
using DLCS.Model.Messaging;
using DLCS.Model.Processing;
using Engine.Ingest;
using Engine.Ingest.Models;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Engine.Tests.Ingest.Handlers;

public class IngestHandlerTests
{
    private readonly IAssetIngester assetIngester;
    private readonly IngestHandler sut;
    private readonly ICustomerQueueRepository customerQueueRepository;

    public IngestHandlerTests()
    {
        assetIngester = A.Fake<IAssetIngester>();
        customerQueueRepository = A.Fake<ICustomerQueueRepository>();
        sut = new IngestHandler(assetIngester, customerQueueRepository, new NullLogger<IngestHandler>());
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
    [InlineData(IngestResultStatus.Failed)]
    [InlineData(IngestResultStatus.Unknown)]
    public async Task HandleMessage_ReturnsTrue_IfFailedOrUnknown(IngestResultStatus result)
    {
        // Arrange
        var body = new JsonObject
        {
            ["created"] = "1985-10-26T09:00:00"
        };
        var queueMessage = new QueueMessage { Body = body, QueueName = "test" };
        A.CallTo(() => assetIngester.Ingest(A<IngestAssetRequest>._, A<CancellationToken>._))
            .Returns(new IngestResult(new AssetId(1 , 2, "fake"), result));
        
        // Act
        var success = await sut.HandleMessage(queueMessage, CancellationToken.None);
        
        // Assert
        A.CallTo(() => assetIngester.Ingest(A<IngestAssetRequest>._, A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() => customerQueueRepository.DecrementSize(A<int>._, A<string>._, A<int>._, A<CancellationToken>._))
            .MustHaveHappened();
        success.Should().BeTrue();
    }
    
    [Theory]
    [InlineData(IngestResultStatus.Success)]
    [InlineData(IngestResultStatus.QueuedForProcessing)]
    public async Task HandleMessage_ReturnsFalse_IfSuccessOrQueued(IngestResultStatus result)
    {
        // Arrange
        var body = new JsonObject
        {
            ["created"] = "1985-10-26T09:00:00"
        };
        var queueMessage = new QueueMessage { Body = body, QueueName = "test" };
        A.CallTo(() => assetIngester.Ingest(A<IngestAssetRequest>._, A<CancellationToken>._))
            .Returns(new IngestResult(new AssetId(1 , 2, "fake"), result));
        
        // Act
        var success = await sut.HandleMessage(queueMessage, CancellationToken.None);
        
        // Assert
        A.CallTo(() => assetIngester.Ingest(A<IngestAssetRequest>._, A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() => customerQueueRepository.DecrementSize(A<int>._, A<string>._, A<int>._, A<CancellationToken>._))
            .MustHaveHappened();
        success.Should().BeTrue();
    }
}