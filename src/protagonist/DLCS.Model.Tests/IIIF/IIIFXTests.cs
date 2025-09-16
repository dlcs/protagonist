using System;
using System.Collections.Generic;
using DLCS.Model.IIIF;
using IIIF;
using IIIF.ImageApi;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Strings;

namespace DLCS.Model.Tests.IIIF;

public class IIIFXTests
{
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

    [Theory]
    [InlineData("max")]
    [InlineData("^max")]
    [InlineData("pct:10")]
    [InlineData("^pct:10")]
    [InlineData("10,10")]
    [InlineData("^10,10")]
    public void IsValidThumbnailParameter_Correct_Invalid(string sizeParam)
        => SizeParameter.Parse(sizeParam).IsValidThumbnailParameter().Should().BeFalse();

    [Theory]
    [InlineData("10,")]
    [InlineData("^10,")]
    [InlineData(",10")]
    [InlineData("^,10")]
    [InlineData("!10,10")]
    [InlineData("^!10,10")]
    public void IsValidThumbnailParameter_Correct_Valid(string sizeParam)
        => SizeParameter.Parse(sizeParam).IsValidThumbnailParameter().Should().BeTrue();

    [Theory]
    [InlineData("max")]
    [InlineData("^max")]
    [InlineData("pct:10")]
    [InlineData("^pct:10")]
    [InlineData("10,10")]
    [InlineData("^10,10")]
    public void Resize_Throws_IfSizeParameter_NotSupported(string sizeParam)
    {
        var sp = SizeParameter.Parse(sizeParam);
        Action action = () => sp.ResizeIfSupported(new Size(10, 20));
        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"Attempt to resize using unsupported SizeParameter: {sizeParam}");
    }

    [Fact]
    public void ToV2Metadata_Correct()
    {
        var dict = new Dictionary<string, string>
        {
            ["first"] = "value 1",
            ["second"] = "value 2",
            ["third"] = "",
        };
        var expected = new List<Metadata>
        {
            new() { Label = new MetaDataValue("first"), Value = new MetaDataValue("value 1") },
            new() { Label = new MetaDataValue("second"), Value = new MetaDataValue("value 2") },
            new() { Label = new MetaDataValue("third"), Value = new MetaDataValue("") },
        };

        dict.ToV2Metadata().Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void ToV3Metadata_Correct()
    {
        const string language = "fr";
        var dict = new Dictionary<string, string>
        {
            ["first"] = "value 1",
            ["second"] = "value 2",
            ["third"] = "",
        };
        
        var expected = new List<LabelValuePair>
        {
            new(new LanguageMap(language, "first"), new LanguageMap(language, "value 1")),
            new(new LanguageMap(language, "second"), new LanguageMap(language, "value 2")),
            new(new LanguageMap(language, "third"), new LanguageMap(language, "")),
        };

        dict.ToV3Metadata(language).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void IsRegionFullOrEquivalent_True_IfFull()
    {
        var region = new RegionParameter { Full = true };
        region.IsFullOrEquivalent(1, 1).Should().BeTrue("Explicitly requesting full image");
    }

    [Fact]
    public void IsRegionFullOrEquivalent_True_IfSquareRegion_AndImageSquare()
    {
        var region = new RegionParameter { Square = true };
        region.IsFullOrEquivalent(100, 100).Should().BeTrue("Requesting square region for square image");
    }
    
    [Theory]
    [InlineData(100, 100)]
    [InlineData(200, 100)]
    [InlineData(100, 200)]
    public void IsRegionFullOrEquivalent_True_IfRequesting0XY_AndWHMatchImage(int w, int h)
    {
        var region = new RegionParameter { X = 0, Y = 0, W = w, H = h };
        region.IsFullOrEquivalent(w, h).Should().BeTrue("Region is for full image");
    }
    
    [Theory]
    [InlineData(100, 100)]
    [InlineData(200, 100)]
    [InlineData(100, 200)]
    public void IsRegionFullOrEquivalent_False_IfRequesting0XY_AndWHMatchImage_ButPercent(int w, int h)
    {
        var region = new RegionParameter { X = 0, Y = 0, W = w, H = h, Percent = true };
        region.IsFullOrEquivalent(w, h).Should().BeFalse("Percent region");
    }
    
    [Theory]
    [InlineData(0, 1, 100, 100)]
    [InlineData(1, 0, 100, 100)]
    [InlineData(1, 1, 100, 100)]
    [InlineData(0, 0, 101, 100)]
    [InlineData(0, 0, 100, 101)]
    [InlineData(0, 0, 101, 101)]
    public void IsRegionFullOrEquivalent_False_IfRequesting0XY_AndWHMatchImage(int x, int y, int w, int h)
    {
        var region = new RegionParameter { X = x, Y = y, W = w, H = h };
        region.IsFullOrEquivalent(100, 100).Should().BeFalse();
    }
}
