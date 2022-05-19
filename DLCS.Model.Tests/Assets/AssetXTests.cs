using System.Collections.Generic;
using DLCS.Core.Types;
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
                new(800, 400),
                new(400, 200),
                new(200, 100),
                new(100, 50),
            });
            maxDimensions.maxBoundedSize.Should().Be(800);
            maxDimensions.maxAvailableWidth.Should().Be(800);
            maxDimensions.maxAvailableHeight.Should().Be(400);
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
                new(800, 400),
                new(400, 200),
                new(200, 100),
                new(100, 50),
            });
            maxDimensions.maxBoundedSize.Should().Be(800);
            maxDimensions.maxAvailableWidth.Should().Be(800);
            maxDimensions.maxAvailableHeight.Should().Be(400);
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
        public void GetAssetId_ReturnsExpected()
        {
            var asset = new Asset { Id = "100/14/image" };
            var expected = new AssetId(100, 14, "image");

            var actual = asset.GetAssetId();

            actual.Should().BeEquivalentTo(expected);
        }
    }
}