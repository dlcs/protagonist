using System;
using System.Collections.Generic;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using FluentAssertions;
using IIIF;
using Xunit;

namespace DLCS.Model.Tests.Assets;

public class AssetXTests
{
    [Fact]
    public void GetAvailableThumbSizes_IncludeUnavailable_Correct_MaxUnauthorisedNoRoles()
    {
        // Arrange
        var thumbnailPolicy = new ThumbnailPolicy
        {
            Id = "TestPolicy",
            Name = "TestPolicy",
            Sizes = "800,400,200,100"
        };

        var asset = new Asset {Width = 5000, Height = 2500, MaxUnauthorised = 500};
        
        // Act
        var sizes = asset.GetAvailableThumbSizes(thumbnailPolicy, out var maxDimensions, true);
        
        // Assert
        sizes.Should().BeEquivalentTo(new List<Size>
        {
            new(800, 400),
            new(400, 200),
            new(200, 100),
            new(100, 50),
        });
        maxDimensions.maxBoundedSize.Should().Be(400);
        maxDimensions.maxAvailableWidth.Should().Be(400);
        maxDimensions.maxAvailableHeight.Should().Be(200);
    }
    
    [Fact]
    public void GetAvailableThumbSizes_NotIncludeUnavailable_Correct_MaxUnauthorisedNoRoles()
    {
        // Arrange
        var thumbnailPolicy = new ThumbnailPolicy
        {
            Id = "TestPolicy",
            Name = "TestPolicy",
            Sizes = "800,400,200,100"
        };

        var asset = new Asset {Width = 5000, Height = 2500, MaxUnauthorised = 500};
        
        // Act
        var sizes = asset.GetAvailableThumbSizes(thumbnailPolicy, out var maxDimensions, false);
        
        // Assert
        sizes.Should().BeEquivalentTo(new List<Size>
        {
            new(400, 200),
            new(200, 100),
            new(100, 50),
        });
        maxDimensions.maxBoundedSize.Should().Be(400);
        maxDimensions.maxAvailableWidth.Should().Be(400);
        maxDimensions.maxAvailableHeight.Should().Be(200);
    }
    
    [Fact]
    public void GetAvailableThumbSizes_IncludeUnavailable_Correct_IfRolesNoMaxUnauthorised()
    {
        // Arrange
        var thumbnailPolicy = new ThumbnailPolicy
        {
            Id = "TestPolicy",
            Name = "TestPolicy",
            Sizes = "800,400,200,100",
        };

        var asset = new Asset {Width = 5000, Height = 2500, Roles = "GoodGuys", MaxUnauthorised = -1};
        
        // Act
        var sizes = asset.GetAvailableThumbSizes(thumbnailPolicy, out var maxDimensions, true);
        
        // Assert
        sizes.Should().BeEquivalentTo(new List<Size>
        {
            new(800, 400),
            new(400, 200),
            new(200, 100),
            new(100, 50),
        });
        maxDimensions.maxBoundedSize.Should().Be(0);
        maxDimensions.maxAvailableWidth.Should().Be(0);
        maxDimensions.maxAvailableHeight.Should().Be(0);
    }
    
    [Fact]
    public void GetAvailableThumbSizes_NotIncludeUnavailable_Correct_IfRolesNoMaxUnauthorised()
    {
        // Arrange
        var thumbnailPolicy = new ThumbnailPolicy
        {
            Id = "TestPolicy",
            Name = "TestPolicy",
            Sizes = "800,400,200,100",
        };

        var asset = new Asset {Width = 5000, Height = 2500, Roles = "GoodGuys", MaxUnauthorised = -1};
        
        // Act
        var sizes = asset.GetAvailableThumbSizes(thumbnailPolicy, out var maxDimensions, false);
        
        // Assert
        sizes.Should().BeNullOrEmpty();
        maxDimensions.maxBoundedSize.Should().Be(0);
        maxDimensions.maxAvailableWidth.Should().Be(0);
        maxDimensions.maxAvailableHeight.Should().Be(0);
    }

    [Fact]
    public void GetAvailableThumbSizes_RestrictsAvailableSizes_IfHasRolesAndMaxUnauthorised()
    {
        // Arrange
        var thumbnailPolicy = new ThumbnailPolicy
        {
            Id = "TestPolicy",
            Name = "TestPolicy",
            Sizes = "800,400,200,100",
        };

        var asset = new Asset {Width = 2500, Height = 5000, Roles = "GoodGuys", MaxUnauthorised = 399};
        
        // Act
        var sizes = asset.GetAvailableThumbSizes(thumbnailPolicy, out var maxDimensions);
        
        // Assert
        sizes.Should().BeEquivalentTo(new List<Size>
        {
            new(100, 200),
            new(50, 100),
        });
        maxDimensions.maxBoundedSize.Should().Be(200);
        maxDimensions.maxAvailableWidth.Should().Be(100);
        maxDimensions.maxAvailableHeight.Should().Be(200);
    }
    
    [Fact]
    public void GetAvailableThumbSizes_ReturnsAvailableAndUnavailableSizes_ButReturnsMaxDimensionsOfAvailableOnly_IfIncludeUnavailable()
    {
        // Arrange
        var thumbnailPolicy = new ThumbnailPolicy
        {
            Id = "TestPolicy",
            Name = "TestPolicy",
            Sizes = "800,400,200,100",
        };

        var asset = new Asset {Width = 2500, Height = 5000, Roles = "GoodGuys", MaxUnauthorised = 399};
        
        // Act
        var sizes = asset.GetAvailableThumbSizes(thumbnailPolicy, out var maxDimensions, true);
        
        // Assert
        sizes.Should().BeEquivalentTo(new List<Size>
        {
            new(400, 800),
            new(200, 400),
            new(100, 200),
            new(50, 100),
        });
        maxDimensions.maxBoundedSize.Should().Be(200);
        maxDimensions.maxAvailableWidth.Should().Be(100);
        maxDimensions.maxAvailableHeight.Should().Be(200);
    }
    
    [Fact]
    public void GetAvailableThumbSizes_HandlesImageBeingSmallerThanThumbnail()
    {
        // Arrange
        var thumbnailPolicy = new ThumbnailPolicy
        {
            Id = "TestPolicy",
            Name = "TestPolicy",
            Sizes = "800,400,200,100"
        };

        var asset = new Asset { Width = 300, Height = 150 };
        
        // Act
        var sizes = asset.GetAvailableThumbSizes(thumbnailPolicy, out var maxDimensions, true);
        
        // Assert
        sizes.Should().BeEquivalentTo(new List<Size>
        {
            new(300, 150),
            new(200, 100),
            new(100, 50),
        });
        maxDimensions.maxBoundedSize.Should().Be(300);
        maxDimensions.maxAvailableWidth.Should().Be(300);
        maxDimensions.maxAvailableHeight.Should().Be(150);
    }
    
    [Fact]
    public void SetFieldsForIngestion_ClearsFields()
    {
        // Arrange
        var asset = new Asset { Error = "I am an error", Ingesting = false };
        var expected = new Asset { Error = string.Empty, Ingesting = true };

        // Act
        asset.SetFieldsForIngestion();
        
        // Assert
        asset.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void MarkAsFinished_SetsFields()
    {
        // Arrange
        var asset = new Asset { Ingesting = true };

        // Act
        asset.MarkAsFinished();
        
        // Assert
        asset.Ingesting.Should().BeFalse();
        asset.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }
}