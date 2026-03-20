using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using API.Infrastructure.Messaging.Adjunct;
using API.Infrastructure.Messaging.General;
using DLCS.AWS.SNS;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.PathElements;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace API.Tests.Infrastructure.Messaging;

// Code for this class is shared heavily with the AssetNotificationSender, so only omitted value tests exist here
public class AdjunctNotificationSenderTests
{
    private readonly ITopicPublisher topicPublisher;
    private readonly IPathCustomerRepository customerPathRepository;
    private readonly AdjunctNotificationSender sut;
    
    public AdjunctNotificationSenderTests()
    {
        topicPublisher = A.Fake<ITopicPublisher>();
        customerPathRepository = A.Fake<IPathCustomerRepository>();
        var notificationSender = new ModificationSender(topicPublisher, customerPathRepository, new  NullLogger<ModificationSender>());

        sut = new AdjunctNotificationSender(notificationSender);
    }
    
    [Fact]
    public async Task SendAssetModifiedMessage_OmitsExpectedProperties()
    {
        var assetId = new AssetId(1, 2, "foo");
        
        var adjunct = new Adjunct()
        {
            Id = "foo",
            MediaType = "something",
            IIIFLink = IIIFLinkType.Annotations,
            AssetId = assetId,
            Type = "something",
            Motivation = "something"
        };

        var assetModifiedRecord = NotificationRecord<Adjunct>.Delete(adjunct, ImageCacheType.Cdn);
        const string customerName = "uno";
        A.CallTo(() => customerPathRepository.GetCustomerPathElement("1"))
            .Returns(new CustomerPathElement(1, customerName));
        
        IReadOnlyList<DeliverableModifiedNotification> payload = null;
        A.CallTo(() =>
                topicPublisher.PublishToAdjunctModifiedTopic(A<IReadOnlyList<DeliverableModifiedNotification>>._,
                    CancellationToken.None))
            .Invokes((IReadOnlyList<DeliverableModifiedNotification> n, CancellationToken _) => payload = n);
        
        // Act
        await sut.SendAdjunctModifiedMessage(assetModifiedRecord, CancellationToken.None);

        // Assert
        payload.Should().HaveCount(1);
        var deleted = JsonNode.Parse(payload.Single().MessageContents)
            .Deserialize<DeliverableDeletedNotification<Adjunct>>(JsonSerializerOptions.Web).Deliverable!;
        deleted.Id.Should().Be(adjunct.Id, "Confirm entire message not cleared");
        deleted.AssetId.Should().Be(adjunct.AssetId, "Confirm entire message not cleared");
        deleted.Asset.Should().BeNull("Asset ignored");
        deleted.Motivation.Should().Be(adjunct.Motivation, "Optional parameters not cleared as well");
    }
}
