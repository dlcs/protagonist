using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Infrastructure.Messaging;
using DLCS.AWS.SNS;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.PathElements;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace API.Tests.Infrastructure.Messaging;

public class AssetNotificationSenderTests
{
    private readonly ITopicPublisher topicPublisher;
    private readonly IPathCustomerRepository customerPathRepository;
    private readonly AssetNotificationSender sut;

    public AssetNotificationSenderTests()
    {
        topicPublisher = A.Fake<ITopicPublisher>();
        customerPathRepository = A.Fake<IPathCustomerRepository>();

        sut = new AssetNotificationSender(topicPublisher, customerPathRepository,
            new NullLogger<AssetNotificationSender>());
    }

    [Fact]
    public async Task SendAssetModifiedMessage_Single_SendsNotification_IfUpdate()
    {
        // Arrange
        var assetModifiedRecord =
            AssetModificationRecord.Update(new Asset(new AssetId(1, 2, "foo")), new Asset(new AssetId(1, 2, "bar")));

        // Act
        await sut.SendAssetModifiedMessage(assetModifiedRecord, CancellationToken.None);
        
        // Assert
        A.CallTo(() =>
            topicPublisher.PublishToAssetModifiedTopic(A<IReadOnlyList<AssetModifiedNotification>>._,
                A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task SendAssetModifiedMessage_Single_SendsNotification_IfCreate()
    {
        // Arrange
        var assetModifiedRecord = AssetModificationRecord.Create(new Asset(new AssetId(1, 2, "foo")));

        // Act
        await sut.SendAssetModifiedMessage(assetModifiedRecord, CancellationToken.None);
        
        // Assert
        A.CallTo(() =>
            topicPublisher.PublishToAssetModifiedTopic(A<IReadOnlyList<AssetModifiedNotification>>._,
                A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task SendAssetModifiedMessage_Single_SendsNotification_IfDelete()
    {
        // Arrange
        var assetModifiedRecord = AssetModificationRecord.Delete(new Asset(new AssetId(1, 2, "foo")), 
            ImageCacheType.Cdn);
        const string customerName = "uno";
        A.CallTo(() => customerPathRepository.GetCustomerPathElement("1"))
            .Returns(new CustomerPathElement(1, customerName));

        // Act
        await sut.SendAssetModifiedMessage(assetModifiedRecord, CancellationToken.None);
        
        // Assert
        A.CallTo(() =>
            topicPublisher.PublishToAssetModifiedTopic(
                A<IReadOnlyList<AssetModifiedNotification>>.That.Matches(n =>
                    n.Single().ChangeType == ChangeType.Delete && n.Single().MessageContents.Contains(customerName)),
                A<CancellationToken>._)).MustHaveHappened();
    }
    
    [Fact]
    public async Task SendAssetModifiedMessage_Multiple_SendsNotification_IfDelete()
    {
        // Arrange
        var deleteFrom = ImageCacheType.Cdn;
        
        var assetModifiedRecord = AssetModificationRecord.Delete(new Asset(new AssetId(1, 2, "foo")), 
            deleteFrom);
        var assetModifiedRecord2 = AssetModificationRecord.Delete(new Asset(new AssetId(1, 2, "bar")),
            deleteFrom);
        var updates = new List<AssetModificationRecord> { assetModifiedRecord, assetModifiedRecord2 };
        const string customerName = "uno";
        A.CallTo(() => customerPathRepository.GetCustomerPathElement("1"))
            .Returns(new CustomerPathElement(1, customerName));

        // Act
        await sut.SendAssetModifiedMessage(updates, CancellationToken.None);
        
        // Assert
        A.CallTo(() =>
            topicPublisher.PublishToAssetModifiedTopic(
                A<IReadOnlyList<AssetModifiedNotification>>.That.Matches(n =>
                    n.Count == 2 && n.All(m =>
                        m.ChangeType == ChangeType.Delete && m.MessageContents.Contains(customerName))),
                A<CancellationToken>._)).MustHaveHappened();
    }
}
