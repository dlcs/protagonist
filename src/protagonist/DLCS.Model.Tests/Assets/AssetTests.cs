using DLCS.Model.Assets;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests.Assets
{
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
        
        [Theory]
        [InlineData("foo-bar")]
        [InlineData("/foo-bar")]
        [InlineData("2/1/foo-bar")]
        public void GetUniqueName_ReturnsExpected(string id)
        {
            // Arrange
            const string expected = "foo-bar";
            var asset = new Asset {Id = id};

            // Assert
            asset.GetUniqueName().Should().Be(expected);
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
}