using DLCS.Model.Assets;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests.Assets
{
    public class AssetTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void RequiresAuth_True_IfHasRolesAndMaxUnauthorisedValue(int maxUnauthorised)
        {
            // Arrange
            var asset = new Asset {Roles = "test", MaxUnauthorised = maxUnauthorised};
            
            // Act
            var actual = asset.RequiresAuth;
            
            // Assert
            actual.Should().BeTrue();
        }
        
        [Theory]
        [InlineData(null, 1)]
        [InlineData("", 2)]
        [InlineData(" ", 1)]
        [InlineData("role", -1)]
        [InlineData("more,roles", -5)]
        public void RequiresAuth_False_IfDoesNotHaveRolesAndMaxUnauthorised(string roles, int maxUnauthorised)
        {
            // Arrange
            var asset = new Asset {Roles = roles, MaxUnauthorised = maxUnauthorised};
            
            // Act
            var actual = asset.RequiresAuth;
            
            // Assert
            actual.Should().BeFalse();
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
    }
}