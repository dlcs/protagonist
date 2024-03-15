using System;
using System.Collections.Generic;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests.Assets;

public class AssetPreparerTests
{
    private readonly char[] restrictedCharacters = Array.Empty<char>();
    
    [Fact]
    public void PrepareAssetForUpsert_NotSuccessful_IfExistingAssetIsNotForDelivery()
    {
        // Arrange
        var existingAsset = new Asset { NotForDelivery = true };
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, new Asset(), false, false, restrictedCharacters);
        
        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void PrepareAssetForUpsert_NotSuccessful_IfChangeFinished_AndAllowNonApiUpdatesFalse()
    {
        // Arrange
        var updateAsset = new Asset { Finished = DateTime.Now, Id = new AssetId(1, 1, "100")  };
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(null, updateAsset, false, false, restrictedCharacters);
        
        // Assert
        result.Success.Should().BeFalse();
    }
    
    [Fact]
    public void PrepareAssetForUpsert_NotSuccessful_IfChangeError_AndAllowNonApiUpdatesFalse()
    {
        // Arrange
        var updateAsset = new Asset { Error = "change", Id = new AssetId(1, 1, "100") };
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(null, updateAsset, false, false, restrictedCharacters);
        
        // Assert
        result.Success.Should().BeFalse();
    }

    [Theory]
    [InlineData("file,iiif-av", "audio/mp4")]
    [InlineData("iiif-av", "audio/mp4")]
    [InlineData("file,iiif-av", "video/mp4")]
    [InlineData("iiif-av", "video/mp4")]
    [InlineData("file", "image/jpeg")]
    public void PrepareAssetForUpsert_CannotUpdateDuration_IfNotFileChannel_AndNotAudioOrVideo(string dc, string mediaType)
    {
        // Arrange
        var updateAsset = new Asset { Duration = 100 };
        var existingAsset = new Asset { MediaType = mediaType, DeliveryChannels = dc.Split(","), Duration = 99 };
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, false, restrictedCharacters);
        
        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Duration cannot be edited.");
    }
    
    [Theory]
    [InlineData("audio/mp4")]
    [InlineData("video/mp4")]
    public void PrepareAssetForUpsert_CanUpdateDuration_IfFileChannel_AndAudioOrVideo(string mediaType)
    {
        // Arrange
        var updateAsset = new Asset { Duration = 100 };
        var existingAsset = new Asset 
        { 
            MediaType = mediaType, 
            ImageDeliveryChannels = new List<ImageDeliveryChannel>()
            {
                new()
                {
                    Channel = AssetDeliveryChannels.File
                }
            }, 
            Duration = 99 
        };

        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, false, restrictedCharacters);
        
        // Assert
        result.Success.Should().BeTrue();
    }
    
    [Theory]
    [InlineData("audio/mp4", "file")]
    [InlineData("image/tiff", "file,iiif-img")]
    [InlineData("image/tiff", "iiif-img")]
    [InlineData("video/mp4", "file,iiif-av")]
    [InlineData("video/mp4", "iiif-av")]
    public void PrepareAssetForUpsert_CannotUpdateWidth_IfNotFileChannel_AndAudio(string mediaType, string dc)
    {
        // Arrange
        var updateAsset = new Asset { Width = 100 };
        var existingAsset = new Asset
        {
            DeliveryChannels = dc.Split(","), MediaType = mediaType, Width = 99
        };
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, false, restrictedCharacters);
        
        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Width cannot be edited.");
    }
    
    [Theory]
    [InlineData("application/pdf", "file")]
    [InlineData("video/mp4", "file")]
    [InlineData("image/tiff", "file")]
    public void PrepareAssetForUpsert_CanUpdateWidth_IfFileChannel_AndNotAudio(string mediaType, string dc)
    {
        // Arrange
        var updateAsset = new Asset { Width = 100 };
        var existingAsset = new Asset { ImageDeliveryChannels = new List<ImageDeliveryChannel>(), MediaType = mediaType, Width = 99 };
        foreach (var channel in dc.Split(","))
        {
            existingAsset.ImageDeliveryChannels.Add(new ImageDeliveryChannel()
            {
                Channel = channel
            });
        }
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, false, restrictedCharacters);
        
        // Assert
        result.Success.Should().BeTrue();
    }
    
    [Theory]
    [InlineData("audio/mp4", "file")]
    [InlineData("image/tiff", "file,iiif-img")]
    [InlineData("image/tiff", "iiif-img")]
    [InlineData("video/mp4", "file,iiif-av")]
    [InlineData("video/mp4", "iiif-av")]
    public void PrepareAssetForUpsert_CannotUpdateHeight_IfNotFileChannel_AndAudio(string mediaType, string dc)
    {
        // Arrange
        var updateAsset = new Asset { Height = 100 };
        var existingAsset = new Asset
        {
            DeliveryChannels = dc.Split(","), MediaType = mediaType, Height = 99
        };
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, false, restrictedCharacters);
        
        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Height cannot be edited.");
    }
    
    [Theory]
    [InlineData("application/pdf", "file")]
    [InlineData("video/mp4", "file")]
    [InlineData("image/tiff", "file")]
    public void PrepareAssetForUpsert_CanUpdateHeight_IfFileChannel_AndNotAudio(string mediaType, string dc)
    {
        // Arrange
        var updateAsset = new Asset { Height = 100 };
        var existingAsset = new Asset { ImageDeliveryChannels = new List<ImageDeliveryChannel>(), MediaType = mediaType, Height = 99 };
        foreach (var channel in dc.Split(","))
        {
            existingAsset.ImageDeliveryChannels.Add(new ImageDeliveryChannel()
            {
                Channel = channel
            });
        }
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, false, restrictedCharacters);
        
        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void PrepareAssetForUpsert_RequiresReingestTrue_IfExistingAssetNull()
    {
        // Arrange
        var updateAsset = new Asset { Origin = "https://whatever", Id = new AssetId(1, 1, "100")  };

        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(null, updateAsset, false, false, restrictedCharacters);

        // Assert
        result.RequiresReingest.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PrepareAssetForUpsert_NotSuccessful_IfRequiresReingest_ButNoOrigin(string origin)
    {
        // Arrange
        var updateAsset = new Asset { Origin = origin, Id = new AssetId(1, 1, "100")  };
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(null, updateAsset, false, false, restrictedCharacters);
        
        // Assert
        result.Success.Should().BeFalse();
    }
    
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void PrepareAssetForUpsert_IsBatchUpdate_DeterminesIfBatchCanBeChanged(bool isBatchUpdate, 
        bool expectedSuccess)
    {
        // Arrange
        var updateAsset = new Asset { Batch = 12 };
        var existingAsset = new Asset { Origin = "foo", Batch = 24 };

        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, isBatchUpdate, restrictedCharacters);
        
        // Assert
        result.Success.Should().Be(expectedSuccess);
    }
    
    [Fact]
    public void PrepareAssetForUpsert_RequiresReingest_IfOriginUpdated()
    {
        // Arrange
        var updateAsset = new Asset { Origin = "https://whatever" };
        var existingAsset = new Asset { Origin = "https://wherever" };
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, false, restrictedCharacters);
        
        // Assert
        result.RequiresReingest.Should().BeTrue();
    }
    
    [Fact]
    public void PrepareAssetForUpsert_RequiresReingest_IfThumbnailPolicyChanged()
    {
        // Arrange
        var updateAsset = new Asset { Origin = "https://whatever", ThumbnailPolicy = "new-policy" };
        var existingAsset = new Asset { Origin = "https://whatever", ThumbnailPolicy = "none" };

        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, false, restrictedCharacters);

        // Assert
        result.RequiresReingest.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(DeliveryChannels))]
    public void PrepareAssetForUpsert_RequiresReingest_IfDeliveryChannelChanged(string[] existing, string[] update,
        string reason)
    {
        // Arrange
        var updateAsset = new Asset { Origin = "https://whatever", ImageDeliveryChannels = GenerateImageDeliveryChannels(update) };
        var existingAsset = new Asset { Origin = "https://whatever", ImageDeliveryChannels =  GenerateImageDeliveryChannels(existing) };

        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, false, restrictedCharacters);

        // Assert
        result.RequiresReingest.Should().BeTrue(reason);
    }

    private List<ImageDeliveryChannel> GenerateImageDeliveryChannels(string[] deliveryChannels)
    {
        var imageDeliveryChannels = new List<ImageDeliveryChannel>();

        foreach (var deliveryChannel in deliveryChannels)
        {
            imageDeliveryChannels.Add(new ImageDeliveryChannel()
            {
                Channel = deliveryChannel
            });
        }

        return imageDeliveryChannels;
    }

    [Theory]
    [InlineData("file", AssetFamily.File)]
    [InlineData("file,iiif-img", AssetFamily.Image)]
    [InlineData("iiif-img", AssetFamily.Image)]
    [InlineData("file,iiif-av", AssetFamily.Timebased)]
    [InlineData("iiif-av", AssetFamily.Timebased)]
    public void PrepareAssetForUpsert_SetsAssetFamilyIfNotSet(string dc, AssetFamily expected)
    {
        // Arrange
        var updateAsset = new Asset { Origin = "required", ImageDeliveryChannels = new List<ImageDeliveryChannel>(), Id = new AssetId(1, 1, "100")  };
        foreach (var channel in dc.Split(","))
        {
            updateAsset.ImageDeliveryChannels.Add(new ImageDeliveryChannel()
            {
                Channel = channel
            });
        }

        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(null, updateAsset, false, false, restrictedCharacters);

        // Assert
        result.UpdatedAsset.Family.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("image/jpeg", AssetFamily.Image)]
    [InlineData("video/mp4", AssetFamily.Timebased)]
    [InlineData("audio/mp4", AssetFamily.Timebased)]
    [InlineData("text/plain", AssetFamily.File)]
    public void PrepareAssetForUpsert_SetsAssetFamily_FromMediaType_IfFamilyAndDeliveryChannelNotSet(string mediaType, AssetFamily expected)
    {
        // Arrange
        var updateAsset = new Asset { Origin = "required", MediaType = mediaType, Id = new AssetId(1, 1, "100")  };

        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(null, updateAsset, false, false, restrictedCharacters);

        // Assert
        result.UpdatedAsset.Family.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("file", AssetFamily.Timebased, AssetFamily.File)]
    [InlineData("file,iiif-img", AssetFamily.Timebased, AssetFamily.Image)]
    [InlineData("iiif-img", AssetFamily.Timebased, AssetFamily.Image)]
    [InlineData("file,iiif-av", AssetFamily.Image, AssetFamily.Timebased)]
    [InlineData("iiif-av", AssetFamily.Image,AssetFamily.Timebased)]
    public void PrepareAssetForUpsert_ChangesAssetFamilyIfSet_New(string dc, AssetFamily current, AssetFamily expected)
    {
        // Arrange
        var updateAsset = new Asset { 
            Origin = "required", 
            ImageDeliveryChannels =new List<ImageDeliveryChannel>(), 
            Family = current, 
            Id = new AssetId(1, 1, "100")
        };
        
        foreach (var channel in dc.Split(","))
        {
            updateAsset.ImageDeliveryChannels.Add(new ImageDeliveryChannel()
            {
                Channel = channel
            });
        }

        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(null, updateAsset, false, false, restrictedCharacters);

        // Assert
        result.UpdatedAsset.Family.Should().Be(expected);
    }

    [Theory]
    [InlineData("file", AssetFamily.Timebased, AssetFamily.File)]
    [InlineData("file,iiif-img", AssetFamily.Timebased, AssetFamily.Image)]
    [InlineData("iiif-img", AssetFamily.Timebased, AssetFamily.Image)]
    [InlineData("file,iiif-av", AssetFamily.Image, AssetFamily.Timebased)]
    [InlineData("iiif-av", AssetFamily.Image, AssetFamily.Timebased)]
    public void PrepareAssetForUpsert_ChangesAssetFamilyIfSet_Update(string dc, AssetFamily current,
        AssetFamily expected)
    {
        // Arrange
        var updateAsset = new Asset { Origin = "required", ImageDeliveryChannels = new List<ImageDeliveryChannel>()};
        foreach (var channel in dc.Split(","))
        {
            updateAsset.ImageDeliveryChannels.Add(new ImageDeliveryChannel()
            {
                Channel = channel
            });
        }
        
        var existingAsset = new Asset
        {
            Family = current, ImageDeliveryChannels = new List<ImageDeliveryChannel>()
            {
                new()
                {
                    Channel = "fake"
                }
            }
        };

        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, false, restrictedCharacters);

        // Assert
        result.UpdatedAsset.Family.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(DeliveryChannels))]
    public void PrepareAssetForUpsert_DoesNotRequiresReingest_IfDeliveryChannelUnchanged(string[] existing, string[] _,
        string __)
    {
        // Arrange
        var updateAsset = new Asset { Origin = "https://whatever", DeliveryChannels = existing };
        var existingAsset = new Asset { Origin = "https://whatever", DeliveryChannels = existing };

        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, false, restrictedCharacters);

        // Assert
        result.RequiresReingest.Should().BeFalse();
    }

    public static IEnumerable<object[]> DeliveryChannels => new List<object[]>
    {
        new object[]
        {
            Array.Empty<string>(), new[] { "iiif-img" }, "set"
        },
        new object[]
        {
            new[] { "iiif-av" }, Array.Empty<string>(), "unset"
        },
        new object[]
        {
            new[] { "file" }, new[] { "iiif-img" }, "single change"
        },
        new object[]
        {
            new[] { "iiif-img" }, new[] { "file", "iiif-img" }, "add new channel"
        },
        new object[]
        {
            new[] { "file", "iiif-av" }, new[] { "iiif-av" }, "remove channel"
        }
    };
}
