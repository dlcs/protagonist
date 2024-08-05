using System;
using System.Collections.Generic;
using DLCS.Model.Assets;
using IIIF;
using IIIF.ImageApi;

namespace DLCS.Model.Tests.Assets;

public class AssetXTests
{
    private readonly List<SizeParameter> sizeParameters = new()
    {
        SizeParameter.Parse("!800,800"),
        SizeParameter.Parse("!400,400"),
        SizeParameter.Parse("!200,200"),
        SizeParameter.Parse("!100,100"),
    };
    
    [Fact]
    public void GetAvailableThumbSizes_Correct_MaxUnauthorisedNoRoles()
    {
        // Arrange
        var asset = new Asset { Width = 5000, Height = 2500, MaxUnauthorised = 500 };

        // Act
        var sizes = asset.GetAvailableThumbSizes(sizeParameters);
        
        // Assert
        sizes.Open.Should().BeEquivalentTo(new List<int[]>
        {
            new[] { 400, 200 },
            new[] { 200, 100 },
            new[] { 100, 50 },
        });
        sizes.Auth.Should().BeEquivalentTo(new List<int[]>
        {
            new[] { 800, 400 },
        });
    }
    
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void GetAvailableThumbSizes_Correct_IfRolesNoMaxUnauthorised(int maxUnauthorised)
    {
        // Arrange
        var asset = new Asset { Width = 5000, Height = 2500, Roles = "GoodGuys", MaxUnauthorised = maxUnauthorised };
        
        // Act
        var sizes = asset.GetAvailableThumbSizes(sizeParameters);
        
        // Assert
        sizes.Open.Should().BeEmpty();
        sizes.Auth.Should().BeEquivalentTo(new List<int[]>
        {
            new[] { 800, 400 },
            new[] { 400, 200 },
            new[] { 200, 100 },
            new[] { 100, 50 },
        });
    }
    
    [Fact]
    public void GetAvailableThumbSizes_Correct_IfRolesMaxUnauthorised()
    {
        // Arrange
        var asset = new Asset { Width = 2500, Height = 5000, Roles = "GoodGuys", MaxUnauthorised = 399 };
        
        // Act
        var sizes = asset.GetAvailableThumbSizes(sizeParameters);
        
        // Assert
        sizes.Open.Should().BeEquivalentTo(new List<int[]>
        {
            new[] { 200, 100 },
            new[] { 100, 50 },
        });
        sizes.Auth.Should().BeEquivalentTo(new List<int[]>
        {
            new[] { 800, 400 },
            new[] { 400, 200 },
        });
    }
    
    [Fact]
    public void GetAvailableThumbSizes_DoesNotReturnDuplicates_IfImageSmallerThanThumbnail()
    {
        // Arrange
        var asset = new Asset { Width = 300, Height = 150 };
        
        // Act
        var sizes = asset.GetAvailableThumbSizes(sizeParameters);
        
        // Assert
        sizes.Open.Should().BeEquivalentTo(new List<int[]>
        {
            new[] { 300, 150 },
            new[] { 200, 100 },
            new[] { 100, 50 },
        });
        sizes.Auth.Should().BeEmpty();
    }
    
    [Fact]
    public void GetAvailableThumbSizes_HandlesNonConfinedSizeParameters_ExcludingDuplicates()
    {
        // Arrange
        var asset = new Asset { Width = 5000, Height = 2500, MaxUnauthorised = 500 };
        var sizeParametersWithNotConfined = new List<SizeParameter>
        {
            SizeParameter.Parse("800,"), // == 800,400
            SizeParameter.Parse(",400"), // == 800,400
            SizeParameter.Parse("!800,800"), // == 800,400
            SizeParameter.Parse("400,"), // == 400,200
        };

        // Act
        var sizes = asset.GetAvailableThumbSizes(sizeParametersWithNotConfined);
        
        // Assert
        sizes.Open.Should().BeEquivalentTo(new List<int[]>
        {
            new[] { 400, 200 },
        });
        sizes.Auth.Should().BeEquivalentTo(new List<int[]>
        {
            new[] { 800, 400 },
        });
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