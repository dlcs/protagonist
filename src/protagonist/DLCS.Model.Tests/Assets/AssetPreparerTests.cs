using System;
using DLCS.Model.Assets;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests.Assets;

public class AssetPreparerTests
{
    [Fact]
    public void PrepareAssetForUpsert_NotSuccessful_IfExistingAssetIsNotForDelivery()
    {
        // Arrange
        var existingAsset = new Asset { NotForDelivery = true };
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, new Asset(), false, false);
        
        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void PrepareAssetForUpsert_NotSuccessful_IfChangeFinished_AndAllowNonApiUpdatesFalse()
    {
        // Arrange
        var updateAsset = new Asset { Finished = DateTime.Now };
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(null, updateAsset, false, false);
        
        // Assert
        result.Success.Should().BeFalse();
    }
    
    [Fact]
    public void PrepareAssetForUpsert_NotSuccessful_IfChangeError_AndAllowNonApiUpdatesFalse()
    {
        // Arrange
        var updateAsset = new Asset { Error = "change" };
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(null, updateAsset, false, false);
        
        // Assert
        result.Success.Should().BeFalse();
    }

    [Theory]
    [InlineData(AssetFamily.File, false)]
    [InlineData(AssetFamily.Image, true)]
    [InlineData(AssetFamily.Timebased, true)]
    public void PrepareAssetForUpsert_CorrectRequiresReingestValue_IfExistingAssetNull_DependingOnFamily(
        AssetFamily family, bool requiresReingest)
    {
        // Arrange
        var updateAsset = new Asset { Origin = "https://whatever", Family = family};

        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(null, updateAsset, false, false);

        // Assert
        result.RequiresReingest.Should().Be(requiresReingest);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PrepareAssetForUpsert_NotSuccessful_IfRequiresReingest_ButNoOrigin(string origin)
    {
        // Arrange
        var updateAsset = new Asset { Origin = origin };
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(null, updateAsset, false, false);
        
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
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, isBatchUpdate);
        
        // Assert
        result.Success.Should().Be(expectedSuccess);
    }
    
    [Theory]
    [InlineData(AssetFamily.File, false)]
    [InlineData(AssetFamily.Image, true)]
    [InlineData(AssetFamily.Timebased, true)]
    public void PrepareAssetForUpsert_RequiresReingest_IfOriginUpdated(AssetFamily family, bool requiresReingest)
    {
        // Arrange
        var updateAsset = new Asset { Origin = "https://whatever" };
        var existingAsset = new Asset { Origin = "https://wherever", Family = family };
        
        // Act
        var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, updateAsset, false, false);
        
        // Assert
        result.RequiresReingest.Should().Be(requiresReingest);
    }
}
