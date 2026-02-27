using System;
using System.Collections.Generic;
using DLCS.Model.Assets;
using IIIF.ImageApi;

namespace DLCS.Model.Tests.Assets;

public class AssetXTests
{
    private readonly List<SizeParameter> sizeParameters =
    [
        SizeParameter.Parse("!800,800"),
        SizeParameter.Parse("!400,400"),
        SizeParameter.Parse("!200,200"),
        SizeParameter.Parse("!100,100"),
    ];
    
    [Fact]
    public void GetAvailableThumbSizes_Correct_MaxWidthNoRoles()
    {
        // Thumbs of 500 or less are open
        var asset = new Asset { Width = 5000, Height = 2500, MaxWidth = 500 };

        // Act
        var sizes = asset.GetAvailableThumbSizes(sizeParameters, 5000);
        
        // Assert
        sizes.Open.Should().BeEquivalentTo((List<int[]>)[[400, 200], [200, 100], [100, 50]]);
        sizes.Auth.Should().BeEquivalentTo((List<int[]>)[[800, 400]]);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void GetAvailableThumbSizes_Correct_IfRolesNoOpenFullMax(int? openFullMax)
    {
        // No thumb sizes are open
        var asset = new Asset { Width = 5000, Height = 2500, Roles = "GoodGuys", OpenFullMax = openFullMax };
        
        // Act
        var sizes = asset.GetAvailableThumbSizes(sizeParameters, 5000);
        
        // Assert
        sizes.Open.Should().BeEmpty();
        sizes.Auth.Should().BeEquivalentTo((List<int[]>)[[800, 400], [400, 200], [200, 100], [100, 50]]);
    }
    
    [Fact]
    public void GetAvailableThumbSizes_Correct_IfRolesOpenFullMax()
    {
        // Only thumbs 399px and below are available
        var asset = new Asset { Width = 2500, Height = 5000, Roles = "GoodGuys", OpenFullMax = 399 };
        
        // Act
        var sizes = asset.GetAvailableThumbSizes(sizeParameters, 5000);
        
        // Assert
        sizes.Open.Should().BeEquivalentTo((List<int[]>)[[200, 100], [100, 50]]);
        sizes.Auth.Should().BeEquivalentTo((List<int[]>)[[800, 400], [400, 200]]);
    }
    
    [Fact]
    public void GetAvailableThumbSizes_DoesNotReturnDuplicates_IfImageSmallerThanThumbnail()
    {
        // Arrange
        var asset = new Asset { Width = 300, Height = 150 };
        
        // Act
        var sizes = asset.GetAvailableThumbSizes(sizeParameters, 5000);
        
        // Assert
        sizes.Open.Should().BeEquivalentTo((List<int[]>)[[300, 150], [200, 100], [100, 50]]);
        sizes.Auth.Should().BeEmpty();
    }
    
    [Fact]
    public void GetAvailableThumbSizes_HandlesNonConfinedSizeParameters_ExcludingDuplicates()
    {
        // Arrange
        var asset = new Asset { Width = 5000, Height = 2500, MaxWidth = 500 };
        var sizeParametersWithNotConfined = new List<SizeParameter>
        {
            SizeParameter.Parse("800,"), // == 800,400
            SizeParameter.Parse(",400"), // == 800,400
            SizeParameter.Parse("!800,800"), // == 800,400
            SizeParameter.Parse("400,"), // == 400,200
        };

        // Act
        var sizes = asset.GetAvailableThumbSizes(sizeParametersWithNotConfined, 5000);
        
        // Assert
        sizes.Open.Should().BeEquivalentTo((List<int[]>)[[400, 200]]);
        sizes.Auth.Should().BeEquivalentTo((List<int[]>)[[800, 400]]);
    }

    [Fact]
    public void GetAvailableThumbSizes_ObeySystemMaxWidth()
    {
        // Thumbs of 500 or less are open as maxWidth is smaller than asset maxWidth
        var asset = new Asset { Width = 5000, Height = 2500, MaxWidth = 5000 };

        // Act
        var sizes = asset.GetAvailableThumbSizes(sizeParameters, 500);

        // Assert
        sizes.Open.Should().BeEquivalentTo((List<int[]>)[[400, 200], [200, 100], [100, 50]]);
        sizes.Auth.Should().BeEquivalentTo((List<int[]>)[[800, 400]]);
    }

    [Theory]
    [InlineData(250, 500, "100,", true, "Ignore width for portrait")]
    [InlineData(500, 250, "100,", false, "Width okay for landscape")]
    [InlineData(250, 250, "100,", false, "Width okay for square")]
    [InlineData(250, 500, "^100,", true, "Ignore confined width for portrait")]
    [InlineData(500, 250, "^100,", false, "Upscale width okay for landscape")]
    [InlineData(250, 250, "^100,", false, "Upscale width okay for square")]
    [InlineData(500, 250, ",100", true, "Ignore height for landscape")]
    [InlineData(250, 500, ",100", false, "Height okay for portrait")]
    [InlineData(250, 250, ",100", false, "Height okay for square")]
    [InlineData(500, 250, "^,100", true, "Ignore height for landscape")]
    [InlineData(250, 500, "^,100", false, "Upscale height okay for portrait")]
    [InlineData(250, 250, "^,100", false, "Upscale height okay for square")]
    [InlineData(500, 250, "!100,100", false, "Confined okay for landscape")]
    [InlineData(250, 500, "!100,100", false, "Confined okay for portrait")]
    [InlineData(250, 250, "!100,100", false, "Confined okay for square")]
    [InlineData(500, 250, "^!100,100", false, "Upscale confined okay for landscape")]
    [InlineData(250, 500, "^!100,100", false, "Upscale confined okay for portrait")]
    [InlineData(250, 250, "^!100,100", false, "Upscale confined okay for square")]
    public void GetAvailableThumbSizes_Ignores_IfMaxDimensionMayVary(int w, int h, string sizeParam, bool ignored, string reason)
    {
        // Arrange
        var asset = new Asset { Width = w, Height = h, };
        var sizeParametersWithNotConfined = new List<SizeParameter>
        {
            SizeParameter.Parse(sizeParam)
        };

        // Act
        var sizes = asset.GetAvailableThumbSizes(sizeParametersWithNotConfined, 4000);
        
        // Assert
        sizes.IsEmpty().Should().Be(ignored, reason);
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

    [Theory]
    [InlineData(null, 5000, 5000, "MaxWidth null (unset), fallback to system default")]
    [InlineData(0, 5000, 5000, "MaxWidth 0 (unset), fallback to system default")]
    [InlineData(512, 5000, 512, "MaxWidth as smaller than system default")]
    [InlineData(8000, 5000, 5000, "System default as smaller than maxWidth")]
    public void GetLargestOpenFullSize_ReturnsMaxWidth_IfNoRoles(int? maxWidth, int systemMaxWidth, int expected,
        string because)
    {
        var asset = new Asset { MaxWidth = maxWidth };
        asset.GetLargestOpenFullSize(systemMaxWidth).Should().Be(expected, because);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void GetLargestOpenFullSize_Returns0_IfOpenFullMaxUnset_AndHasRoles(int? openFullMax)
    {
        var asset = new Asset { OpenFullMax = openFullMax, Roles = "https://test.role" };
        asset.GetLargestOpenFullSize(1000).Should().Be(0);
    }
    
    [Theory]
    [InlineData(1000, null, 5000, 1000, "OpenFullMax as smallest (MaxWidth null = unset)")]
    [InlineData(1000, 0, 5000, 1000, "OpenFullMax as smallest (MaxWidth 0 = unset)")]
    [InlineData(1000, 512, 5000, 512, "MaxWidth as smallest")]
    [InlineData(1000, 8000, 500, 500, "System default as smallest")]
    public void GetLargestOpenFullSize_ReturnsSmallestOfAvailableValues_IfHasRoles(int? openFullMax, int? maxWidth, int systemMaxWidth, int expected,
        string because)
    {
        var asset = new Asset { MaxWidth = maxWidth, OpenFullMax = openFullMax, Roles = "https://test.role" };
        asset.GetLargestOpenFullSize(systemMaxWidth).Should().Be(expected, because);
    }

    [Theory]
    [InlineData(0, 5000, 5000)]
    [InlineData(5000, 5000, 5000)]
    [InlineData(50000, 5000, 5000)]
    public void GetEffectiveMaxWidth_Correct(int assetMaxWidth, int systemMaxWidth, int expected)
    {
        var asset = new Asset { MaxWidth = assetMaxWidth };
        asset.GetEffectiveMaxWidth(systemMaxWidth).Should().Be(expected);
    }
}
