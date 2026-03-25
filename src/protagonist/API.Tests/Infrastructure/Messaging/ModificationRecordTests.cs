using API.Infrastructure.Messaging.General;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;

namespace API.Tests.Infrastructure.Messaging;

public class ModificationRecordTests
{
    [Fact]
    public void Delete_SetsCorrectFields()
    {
        // Arrange
        var asset = new Asset { Id = new AssetId(1, 2, "foo") };
        
        // Act
        var notification = NotificationRecord<Asset>.Delete(asset, ImageCacheType.Cdn);
        
        // Assert
        notification.ChangeType.Should().Be(ChangeType.Delete);
        notification.Before.Should().Be(asset);
        notification.After.Should().BeNull();
    }
    
    [Fact]
    public void Create_SetsCorrectFields()
    {
        // Arrange
        var asset = new Asset { Id = new AssetId(1, 2, "foo") };
        
        // Act
        var notification = NotificationRecord<Asset>.Create(asset);
        
        // Assert
        notification.ChangeType.Should().Be(ChangeType.Create);
        notification.After.Should().Be(asset);
        notification.Before.Should().BeNull();
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Update_SetsCorrectFields(bool engineNotified)
    {
        // Arrange
        var before = new Asset { Id = new AssetId(1, 2, "foo") };
        var after = new Asset { Id = new AssetId(1, 2, "foo"), OpenFullMax = 10 };
        
        // Act
        var notification = NotificationRecord<Asset>.Update(before, after, engineNotified);
        
        // Assert
        notification.ChangeType.Should().Be(ChangeType.Update);
        notification.Before.Should().Be(before);
        notification.After.Should().Be(after);
        notification.EngineNotified.Should().Be(engineNotified);
    }
}
