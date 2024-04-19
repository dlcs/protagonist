using System.Collections.Generic;
using DLCS.Model.IIIF;
using IIIF;
using IIIF.ImageApi;

namespace DLCS.Model.Tests.IIIF;

public class IIIFXTests
{
    [Theory]
    [InlineData(null, null, 0)]
    [InlineData(100, null, 100)]
    [InlineData(null, 100, 100)]
    [InlineData(200, 100, 200)]
    public void GetMaxDimension_Correct(int? width, int? height, int expected)
    {
        var sizeParameter = new SizeParameter { Width = width, Height = height };
        sizeParameter.GetMaxDimension().Should().Be(expected);
    }

    [Fact]
    public void SizeClosestTo_Correct_MatchingLongestEdge()
    {
        // Arrange
        var tooSmall = new Size(10, 20);
        var expected = new Size(100, 200);
        var matchMinDimension = new Size(200, 400);
        var candidates = new List<Size> { tooSmall, expected, matchMinDimension, };

        // Act
        var actual = candidates.SizeClosestTo(200);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void SizeClosestTo_Correct_ClosestSmaller()
    {
        // Arrange
        var tooSmall = new Size(10, 20);
        var expected = new Size(100, 200);
        var tooLarge = new Size(200, 400);
        var candidates = new List<Size> { tooSmall, expected, tooLarge, };

        // Act
        var actual = candidates.SizeClosestTo(250);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void SizeClosestTo_Correct_ClosestLarger()
    {
        // Arrange
        var tooSmall = new Size(10, 20);
        var small = new Size(100, 200);
        var expected = new Size(200, 400);
        var candidates = new List<Size> { tooSmall, small, expected, };

        // Act
        var actual = candidates.SizeClosestTo(350);
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void SizeClosestTo_PicksLargerSize_IfEquidistant()
    {
        // Arrange
        var smaller = new Size(100, 200);
        var expected = new Size(200, 400);
        var candidates = new List<Size> { expected, smaller, };

        // Act
        var actual = candidates.SizeClosestTo(300);
        
        // Assert
        actual.Should().Be(expected);
    }
}