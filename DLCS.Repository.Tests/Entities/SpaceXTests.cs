using DLCS.Repository.Entities;
using FluentAssertions;
using Xunit;

namespace DLCS.Repository.Tests.Entities
{
    public class SpaceXTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AddTag_Adds_IfTagEmpty(string tags)
        {
            // Arrange
            var space = new Space {Tags = tags};
            
            // Arrange
            space.AddTag("foo");
            
            // Assert
            space.Tags.Should().Be("foo");
        }

        [Fact]
        public void AddTag_Adds_IfTagHasValues()
        {
            // Arrange
            var space = new Space {Tags = "bar,baz"};
            
            // Arrange
            space.AddTag("foo");
            
            // Assert
            space.Tags.Should().Be("bar,baz,foo");
        }

        [Fact]
        public void AddTag_Noop_IfTagExists()
        {
            // Arrange
            var space = new Space {Tags = "bar,baz"};
            
            // Arrange
            space.AddTag("bar");
            
            // Assert
            space.Tags.Should().Be("bar,baz");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void RemoveTag_Noop_IfTagsEmpty(string tags)
        {
            // Arrange
            var space = new Space{Tags = tags};
            
            // Arrange
            space.RemoveTag("foo");
            
            // Assert
            space.Tags.Should().BeNullOrEmpty();
        }

        [Fact]
        public void RemoveTag_Noop_IfTagDoesntExist()
        {
            // Arrange
            var space = new Space {Tags = "bar,baz"};
            
            // Arrange
            space.RemoveTag("foo");
            
            // Assert
            space.Tags.Should().Be("bar,baz");
        }

        [Fact]
        public void RemoveTag_Removes_IfTagExists()
        {
            // Arrange
            var space = new Space {Tags = "bar,baz"};
            
            // Arrange
            space.RemoveTag("bar");
            
            // Assert
            space.Tags.Should().Be("baz");
        }
    }
}