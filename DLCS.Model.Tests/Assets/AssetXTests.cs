using System.Collections.Generic;
using DLCS.Model.Assets;
using FluentAssertions;
using IIIF;
using Xunit;

namespace DLCS.Model.Tests.Assets
{
    public class AssetXTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetAvailableThumbSizes_ReturnsAllSizes_RegardlessOfIncludeAvailable_IfNoRoles(bool includeUnavailable)
        {
            // Arrange
            var thumbnailPolicy = new ThumbnailPolicy
            {
                Id = "TestPolicy",
                Name = "TestPolicy",
                Sizes = "800,400,200,100"
            };

            var asset = new Asset {Width = 5000, Height = 2500, MaxUnauthorised = 50};
            
            // Act
            var sizes = asset.GetAvailableThumbSizes(thumbnailPolicy, out var maxDimensions, includeUnavailable);
            
            // Assert
            sizes.Should().BeEquivalentTo(new List<Size>
            {
                new Size(800, 400),
                new Size(400, 200),
                new Size(200, 100),
                new Size(100, 50),
            });
            maxDimensions.maxBoundedSize = 800;
            maxDimensions.maxAvailableWidth = 800;
            maxDimensions.maxAvailableHeight = 400;
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetAvailableThumbSizes_ReturnsAllSizes_RegardlessOfIncludeAvailable_IfRolesButNoMaxUnauth(bool includeUnavailable)
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
            var sizes = asset.GetAvailableThumbSizes(thumbnailPolicy, out var maxDimensions, includeUnavailable);
            
            // Assert
            sizes.Should().BeEquivalentTo(new List<Size>
            {
                new Size(800, 400),
                new Size(400, 200),
                new Size(200, 100),
                new Size(100, 50),
            });
            maxDimensions.maxBoundedSize = 800;
            maxDimensions.maxAvailableWidth = 800;
            maxDimensions.maxAvailableHeight = 400;
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
                new Size(100, 200),
                new Size(50, 100),
            });
            maxDimensions.maxBoundedSize = 200;
            maxDimensions.maxAvailableWidth = 100;
            maxDimensions.maxAvailableHeight = 200;
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
                new Size(400, 800),
                new Size(200, 400),
                new Size(100, 200),
                new Size(50, 100),
            });
            maxDimensions.maxBoundedSize = 200;
            maxDimensions.maxAvailableWidth = 100;
            maxDimensions.maxAvailableHeight = 200;
        }
    }
}