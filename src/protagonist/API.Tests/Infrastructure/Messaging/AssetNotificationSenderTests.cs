using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DLCS.AWS.SNS;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.PathElements;
using DLCS.Model.Policies;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using API.Infrastructure;
using API.Infrastructure.Messaging.General;

namespace API.Tests.Infrastructure.Messaging;

public class DeliverableNotificationSenderTests
{
    private readonly ITopicPublisher topicPublisher;
    private readonly IPathCustomerRepository customerPathRepository;
    private readonly DeliverableNotificationSender sut;

    public DeliverableNotificationSenderTests()
    {
        topicPublisher = A.Fake<ITopicPublisher>();
        customerPathRepository = A.Fake<IPathCustomerRepository>();
        var notificationSender = new ModificationSender(topicPublisher, customerPathRepository, new  NullLogger<ModificationSender>());

        sut = new DeliverableNotificationSender(notificationSender);
    }

    [Fact]
    public async Task SendDeliverableModifiedMessage_Asset_Single_SendsNotification_IfUpdate()
    {
        // Arrange
        var assetModifiedRecord =
            NotificationRecord<Asset>.Update(new Asset(new AssetId(1, 2, "foo")), new Asset(new AssetId(1, 2, "bar")), true);

        // Act
        await sut.SendDeliverableModifiedMessage(assetModifiedRecord, CancellationToken.None);
        
        // Assert
        A.CallTo(() =>
            topicPublisher.PublishToDeliverableModifiedTopic(A<IReadOnlyList<DeliverableModifiedNotification>>._,
                DeliverableTopicType.Asset, A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task SendDeliverableModifiedMessage_Asset_Single_SendsNotification_IfCreate()
    {
        // Arrange
        var assetModifiedRecord = NotificationRecord<Asset>.Create(new Asset(new AssetId(1, 2, "foo")));

        // Act
        await sut.SendDeliverableModifiedMessage(assetModifiedRecord, CancellationToken.None);
        
        // Assert
        A.CallTo(() =>
            topicPublisher.PublishToDeliverableModifiedTopic(A<IReadOnlyList<DeliverableModifiedNotification>>._,
                DeliverableTopicType.Asset, A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task SendDeliverableModifiedMessage_Asset_Single_SendsNotification_IfDelete()
    {
        // Arrange
        var assetModifiedRecord = NotificationRecord<Asset>.Delete(new Asset(new AssetId(1, 2, "foo")), 
            ImageCacheType.Cdn);
        const string customerName = "uno";
        A.CallTo(() => customerPathRepository.GetCustomerPathElement("1"))
            .Returns(new CustomerPathElement(1, customerName));

        // Act
        await sut.SendDeliverableModifiedMessage(assetModifiedRecord, CancellationToken.None);
        
        // Assert
        A.CallTo(() =>
            topicPublisher.PublishToDeliverableModifiedTopic(
                A<IReadOnlyList<DeliverableModifiedNotification>>.That.Matches(n =>
                    n.Single().Attributes.Values.Contains(ChangeType.Delete.ToString()) && n.Single().MessageContents.Contains(customerName)), 
                DeliverableTopicType.Asset, A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task SendDeliverableModifiedMessage_Asset_Multiple_SendsNotification_IfDelete()
    {
        var deleteFrom = ImageCacheType.Cdn;
        // Arrange
        var assetModifiedRecord = NotificationRecord<Asset>.Delete(new Asset(new AssetId(1, 2, "foo")), 
            deleteFrom);
        var assetModifiedRecord2 = NotificationRecord<Asset>.Delete(new Asset(new AssetId(1, 2, "bar")),
            deleteFrom);
        var updates = new List<NotificationRecord<Asset>> { assetModifiedRecord, assetModifiedRecord2 };
        const string customerName = "uno";
        A.CallTo(() => customerPathRepository.GetCustomerPathElement("1"))
            .Returns(new CustomerPathElement(1, customerName));

        // Act
        await sut.SendDeliverableModifiedMessage(updates, CancellationToken.None);
        
        // Assert
        A.CallTo(() =>
            topicPublisher.PublishToDeliverableModifiedTopic(
                A<IReadOnlyList<DeliverableModifiedNotification>>.That.Matches(n =>
                    n.Count == 2 && n.All(m =>
                        n.First().Attributes.Values.Contains(ChangeType.Delete.ToString()) && m.MessageContents.Contains(customerName))),
                DeliverableTopicType.Asset, A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task SendDeliverableModifiedMessage_Asset_OmitsExpectedProperties()
    {
        var asset = new Asset(new AssetId(1, 2, "foo"))
        {
            ImageOptimisationPolicy = "test",
            ThumbnailPolicy = "100,200,300",
            BatchAssets = [new()],
            ImageDeliveryChannels = [new ImageDeliveryChannel
            {
                DeliveryChannelPolicyId = 1234,
                Channel = AssetDeliveryChannels.Timebased,
                DeliveryChannelPolicy = new DeliveryChannelPolicy
                {
                    Id = 1234,
                    Channel = AssetDeliveryChannels.Image
                }
            }],
            Adjuncts = [
                new Adjunct
                {
                    Id = "someAdjunctId",
                    MediaType = "image/jpeg",
                    IIIFLink = IIIFLinkType.SeeAlso,
                    Type = "Image",
                    AssetId = new AssetId(1, 2, "foo"),
                    ExternalId = new Uri("https://somewhere/somePath")
                }
            ]
        };

        var assetModifiedRecord = NotificationRecord<Asset>.Delete(asset, ImageCacheType.Cdn);
        const string customerName = "uno";
        A.CallTo(() => customerPathRepository.GetCustomerPathElement("1"))
            .Returns(new CustomerPathElement(1, customerName));
        
        IReadOnlyList<DeliverableModifiedNotification> payload = null;
        A.CallTo(() =>
                topicPublisher.PublishToDeliverableModifiedTopic(A<IReadOnlyList<DeliverableModifiedNotification>>._,
                    DeliverableTopicType.Asset, CancellationToken.None))
            .Invokes((IReadOnlyList<DeliverableModifiedNotification> n,  DeliverableTopicType _, CancellationToken _) => payload = n);
        
        // Act
        await sut.SendDeliverableModifiedMessage(assetModifiedRecord, CancellationToken.None);

        // Assert
        payload.Should().HaveCount(1);
        var deleted = JsonNode.Parse(payload.Single().MessageContents)
            .Deserialize<DeliverableDeletedNotification<Asset>>(JsonSerializerOptions.Web).Deliverable!;
        deleted.Id.Should().Be(asset.Id, "Confirm entire message not cleared");
        deleted.BatchAssets.Should().BeNull("BatchAsset ignored");
        deleted.ImageOptimisationPolicy.Should().BeNull("ImageOptimisationPolicy ignored");
        deleted.ThumbnailPolicy.Should().BeNull("ThumbnailPolicy ignored");
        deleted.Adjuncts.Should().BeNull("Adjuncts ignored");
        deleted.ImageDeliveryChannels.Should().HaveCount(1, "ImageDeliveryChannels NOT ignored")
            .And.Subject.Single().DeliveryChannelPolicy.Should().BeNull("DeliveryChannelPolicy ignored");
    }
    
    [Fact]
    public async Task SendDeliverableModifiedMessage_Adjunct_OmitsExpectedProperties()
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
                topicPublisher.PublishToDeliverableModifiedTopic(A<IReadOnlyList<DeliverableModifiedNotification>>._,
                    DeliverableTopicType.Adjunct, CancellationToken.None))
            .Invokes((IReadOnlyList<DeliverableModifiedNotification> n, DeliverableTopicType _,  CancellationToken _) => payload = n);
        
        // Act
        await sut.SendDeliverableModifiedMessage(assetModifiedRecord, CancellationToken.None);

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
