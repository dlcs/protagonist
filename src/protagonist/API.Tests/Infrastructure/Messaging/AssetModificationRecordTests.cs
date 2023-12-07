using System.Collections.Generic;
using API.Infrastructure.Messaging;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;

namespace API.Tests.Infrastructure.Messaging;

public class AssetModificationRecordTests
{
    [Fact]
    public void Delete_SetsCorrectFields()
    {
        // Arrange
        var asset = new Asset { Id = new AssetId(1, 2, "foo") };
        
        // Act
        var notification = AssetModificationRecord.Delete(asset, new List<string>() { "cdn" });
        
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
        var notification = AssetModificationRecord.Create(asset);
        
        // Assert
        notification.ChangeType.Should().Be(ChangeType.Create);
        notification.After.Should().Be(asset);
        notification.Before.Should().BeNull();
    }
    
    [Fact]
    public void Update_SetsCorrectFields()
    {
        // Arrange
        var before = new Asset { Id = new AssetId(1, 2, "foo") };
        var after = new Asset { Id = new AssetId(1, 2, "foo"), MaxUnauthorised = 10 };
        
        // Act
        var notification = AssetModificationRecord.Update(before, after);
        
        // Assert
        notification.ChangeType.Should().Be(ChangeType.Update);
        notification.Before.Should().Be(before);
        notification.After.Should().Be(after);
    }
}