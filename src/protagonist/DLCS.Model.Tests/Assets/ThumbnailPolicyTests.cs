using System.Collections.Generic;
using DLCS.Model.Policies;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests.Assets;

public class ThumbnailPolicyTests
{
    [Fact]
    public void SizesList_ReturnsCommaDelimitedSizes_IfHasSizes()
    {
        // Arrange
        var thumbnailPolicy = new ThumbnailPolicy
        {
            Id = "TestPolicy",
            Name = "TestPolicy",
            Sizes = "800,400,200,100"
        };
        
        // Assert
        thumbnailPolicy.SizeList.Should().BeEquivalentTo(new List<int> { 800, 400, 200, 100 });
    }
    
    [Fact]
    public void SizesList_ReturnsCommaDelimitedSizes_InOrder_IfHasSizes()
    {
        // Arrange
        var thumbnailPolicy = new ThumbnailPolicy
        {
            Id = "TestPolicy",
            Name = "TestPolicy",
            Sizes = "800,200,100,400"
        };
        
        // Assert
        thumbnailPolicy.SizeList.Should().BeEquivalentTo(new List<int> { 800, 400, 200, 100 });
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SizesList_ReturnsNull__IfNoSizes(string sizes)
    {
        // Arrange
        var thumbnailPolicy = new ThumbnailPolicy
        {
            Id = "TestPolicy",
            Name = "TestPolicy",
            Sizes = sizes
        };
        
        // Assert
        thumbnailPolicy.SizeList.Should().BeEmpty();
    }
}