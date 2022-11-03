using DLCS.Core.Types;
using DLCS.Model.Assets;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests.Assets;

public class AssetTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData("", 2)]
    [InlineData(" ", 1)]
    [InlineData("role", 0)]
    [InlineData("role", 1)]
    [InlineData("role", -1)]
    [InlineData("more,roles", -5)]
    public void RequiresAuth_True_IfHaveRolesOrMaxUnauthorised(string roles, int maxUnauthorised)
    {
        // Arrange
        var asset = new Asset {Roles = roles, MaxUnauthorised = maxUnauthorised};
        
        // Act
        var actual = asset.RequiresAuth;
        
        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void Ctor_SetsCustomerAndSpace()
    {
        // Arrange
        const int customer = 10;
        const int space = 50;
        const string asset = "foo";
        var assetId = new AssetId(customer, space, asset);
        
        // Act
        var constructed = new Asset(assetId);
        
        // Assert
        constructed.Id.Should().Be(assetId);
        constructed.Customer.Should().Be(assetId.Customer);
        constructed.Space.Should().Be(assetId.Space);
    }

    [Fact]
    public void Roles_Convert_To_List()
    {
        var asset = new Asset { Roles = "a,b,c" };
        var expected = new[] { "a", "b", "c" };
        asset.RolesList.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void Roles_Convert_From_List()
    {
        var asset = new Asset { RolesList = new[] { "a", "b", "c" } };
        var expected = "a,b,c";
        asset.Roles.Should().Be(expected);
    }
    
    [Fact]
    public void Tags_Convert_To_List()
    {
        var asset = new Asset { Tags = "a,b,c" };
        var expected = new[] { "a", "b", "c" };
        asset.TagsList.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Tags_Convert_From_List()
    {
        var asset = new Asset { TagsList = new[] { "a", "b", "c" } };
        var expected = "a,b,c";
        asset.Tags.Should().Be(expected);
    }
}