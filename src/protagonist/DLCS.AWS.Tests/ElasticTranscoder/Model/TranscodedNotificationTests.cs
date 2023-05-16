﻿using DLCS.AWS.ElasticTranscoder.Models;
using DLCS.Core.Types;

namespace DLCS.AWS.Tests.ElasticTranscoder.Model;

public class TranscodedNotificationTests
{
    [Fact]
    public void GetAssetId_Null_IfNotFound()
    {
        // Arrange
        var notification = new TranscodedNotification { UserMetadata = new Dictionary<string, string>() };
        
        // Act
        var result = notification.GetAssetId();
        
        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-id")]
    public void GetAssetId_Null_IfEmptyOrInvalid(string metadataValue)
    {
        // Arrange
        var notification = new TranscodedNotification
        {
            UserMetadata = new Dictionary<string, string>
            {
                ["dlcsId"] = metadataValue
            }
        };
        
        // Act
        var result = notification.GetAssetId();
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public void GetAssetId_ReturnsAssetId_IfFound()
    {
        // Arrange
        var expected = new AssetId(7, 88, "kyogo");
        var notification = new TranscodedNotification
        {
            UserMetadata = new Dictionary<string, string>
            {
                ["dlcsId"] = "7/88/kyogo"
            }
        };
        
        // Act
        var result = notification.GetAssetId();
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }
}