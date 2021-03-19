using System.Collections.Generic;
using DLCS.Repository.Assets;
using FluentAssertions;
using IIIF;
using IIIF.ImageApi;
using Xunit;

namespace DLCS.Repository.Tests.Assets
{
    public class ThumbnailCalculatorTests
    {
        private readonly List<Size> landscapeSizes;
        private readonly List<Size> portraitSizes;

        public ThumbnailCalculatorTests()
        {
            portraitSizes = new List<Size>
            {
                new Size(400, 800),
                new Size(200, 400),
                new Size(100, 200),
            };

            landscapeSizes = new List<Size>
            {
                new Size(800, 400),
                new Size(400, 200),
                new Size(200, 100),
            };
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetCandidates_ReturnsCorrectLongestEdge_IfSizeAndHeightProvided(bool resize)
        {
            // Arrange
            var imageRequest = new ImageRequest
            {
                Size = new SizeParameter
                {
                    Width = 110,
                    Height = 200
                }
            };
            
            // Act
            var result = ThumbnailCalculator.GetCandidate(portraitSizes, imageRequest, resize);
            
            // Assert
            result.KnownSize.Should().BeTrue();
            result.LongestEdge.Should().Be(200);
        }
        
        [Fact]
        public void GetCandidates_NoResize_ReturnsEmptyLongestEdge_IfSizeAndHeightProvidedButNotFound()
        {
            // Arrange
            var imageRequest = new ImageRequest
            {
                Size = new SizeParameter
                {
                    Width = 110,
                    Height = 210
                }
            };
            
            // Act
            var result = ThumbnailCalculator.GetCandidate(portraitSizes, imageRequest, false);
            
            // Assert
            result.KnownSize.Should().BeFalse();
            result.LongestEdge.Should().BeNull();
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetCandidates_ReturnsCorrectLongestEdge_IfWidthProvidedAndFound(bool resize)
        {
            // Arrange
            var imageRequest = new ImageRequest {Size = new SizeParameter {Width = 200,}};
            
            // Act
            var result = ThumbnailCalculator.GetCandidate(landscapeSizes, imageRequest, resize);
            
            // Assert
            result.KnownSize.Should().BeTrue();
            result.LongestEdge.Should().Be(200);
        }
        
        [Fact]
        public void GetCandidates_NoResize_ReturnsEmptyLongestEdge_IfWidthProvidedAndNotFound()
        {
            // Arrange
            var imageRequest = new ImageRequest {Size = new SizeParameter {Width = 210,}};
            
            // Act
            var result = ThumbnailCalculator.GetCandidate(landscapeSizes, imageRequest, false);
            
            // Assert
            result.KnownSize.Should().BeFalse();
            result.LongestEdge.Should().BeNull();
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetCandidates_ReturnsCorrectLongestEdge_IfHeightProvidedAndFound(bool resize)
        {
            // Arrange
            var imageRequest = new ImageRequest {Size = new SizeParameter {Height = 200,}};
            
            // Act
            var result = ThumbnailCalculator.GetCandidate(landscapeSizes, imageRequest, resize);
            
            // Assert
            result.KnownSize.Should().BeTrue();
            result.LongestEdge.Should().Be(400);
        }
        
        [Fact]
        public void GetCandidates_NoResize_ReturnsEmptyLongestEdge_IfHeightProvidedAndNotFound()
        {
            // Arrange
            var imageRequest = new ImageRequest {Size = new SizeParameter {Height = 210,}};
            
            // Act
            var result = ThumbnailCalculator.GetCandidate(landscapeSizes, imageRequest, false);
            
            // Assert
            result.KnownSize.Should().BeFalse();
            result.LongestEdge.Should().BeNull();
        }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetCandidates_ReturnsLargestLongestEdge_IfMax(bool resize)
        {
            // Arrange
            var imageRequest = new ImageRequest {Size = new SizeParameter {Max = true,}};
            
            // Act
            var result = ThumbnailCalculator.GetCandidate(landscapeSizes, imageRequest, resize);
            
            // Assert
            result.KnownSize.Should().BeTrue();
            result.LongestEdge.Should().Be(800);
        }

        [Fact]
        public void GetCandidates_Resize_ReturnsResizeCandidate_WithLargerAndSmaller_IfExactMatchNotFound()
        {
            // Arrange
            var imageRequest = new ImageRequest {Size = new SizeParameter {Width = 300}};
            
            // Act
            var result = (ResizableSize)ThumbnailCalculator.GetCandidate(portraitSizes, imageRequest, true);
            
            // Assert
            result.KnownSize.Should().BeFalse();
            result.LongestEdge.Should().BeNull();
            result.Ideal.Height.Should().Be(600);
            result.Ideal.Width.Should().Be(300);
            result.LargerSize.Height.Should().Be(800);
            result.LargerSize.Width.Should().Be(400);
            result.SmallerSize.Height.Should().Be(400);
            result.SmallerSize.Width.Should().Be(200);
        }
        
        [Fact]
        public void GetCandidates_Resize_ReturnsResizeCandidate_WithLarger_IfExactMatchNotFound()
        {
            // Arrange
            var imageRequest = new ImageRequest {Size = new SizeParameter {Width = 50}};
            
            // Act
            var result = (ResizableSize)ThumbnailCalculator.GetCandidate(portraitSizes, imageRequest, true);
            
            // Assert
            result.KnownSize.Should().BeFalse();
            result.LongestEdge.Should().BeNull();
            result.Ideal.Height.Should().Be(100);
            result.Ideal.Width.Should().Be(50);
            result.LargerSize.Height.Should().Be(200);
            result.LargerSize.Width.Should().Be(100);
            result.SmallerSize.Should().BeNull();
        }
        
        [Fact]
        public void GetCandidates_Resize_ReturnsResizeCandidate_WithSmaller_IfExactMatchNotFound()
        {
            // Arrange
            var imageRequest = new ImageRequest {Size = new SizeParameter {Width = 500}};
            
            // Act
            var result = (ResizableSize)ThumbnailCalculator.GetCandidate(portraitSizes, imageRequest, true);
            
            // Assert
            result.KnownSize.Should().BeFalse();
            result.LongestEdge.Should().BeNull();
            result.Ideal.Height.Should().Be(1000);
            result.Ideal.Width.Should().Be(500);
            result.SmallerSize.Height.Should().Be(800);
            result.SmallerSize.Width.Should().Be(400);
            result.LargerSize.Should().BeNull();
        }

        [Fact]
        public void GetCandidates_Resize_ReturnsCorrectIdealSize_IfConfinedSquareRequested_ForNonSquareImage()
        {
            // Arrange
            var imageRequest = new ImageRequest
            {
                Size = new SizeParameter {Height = 400, Width = 400, Confined = true},
            };
            
            // Act
            var smallSizes = new List<Size> {new Size(200, 195), new Size(100, 97)};
            var result = (ResizableSize)ThumbnailCalculator.GetCandidate(smallSizes, imageRequest, true);
            
            // Assert
            result.KnownSize.Should().BeFalse();
            result.LongestEdge.Should().BeNull();
            result.Ideal.Width.Should().Be(400);
            result.Ideal.Height.Should().Be(390);
            result.SmallerSize.Height.Should().Be(195);
            result.SmallerSize.Width.Should().Be(200);
            result.LargerSize.Should().BeNull();
        }
    }
}
