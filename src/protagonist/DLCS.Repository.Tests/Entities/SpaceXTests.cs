using System;
using DLCS.Model.Spaces;
using DLCS.Repository.Entities;
using FluentAssertions;
using Xunit;

namespace DLCS.Repository.Tests.Entities;

public class SpaceXTests
{
    [Theory]
    [InlineData(null)]
    public void AddTag_Adds_IfTagEmpty(string[] tags)
    {
        // Arrange
        var space = new Space {Tags = tags};
        
        // Arrange
        space.AddTag("foo");
        
        // Assert
        space.Tags.Should().Contain("foo");
    }

    [Fact]
    public void AddTag_Adds_IfTagHasValues()
    {
        // Arrange
        var space = new Space {Tags = new[]{ "bar", "baz"}};
        
        // Arrange
        space.AddTag("foo");
        
        // Assert
        space.Tags.Should().BeEquivalentTo("bar", "baz", "foo");
    }

    [Fact]
    public void AddTag_Noop_IfTagExists()
    {
        // Arrange
        var space = new Space {Tags = new[]{ "bar", "baz"}};
        
        // Arrange
        space.AddTag("bar");
        
        // Assert
        space.Tags.Should().BeEquivalentTo("bar", "baz");
    }

    [Theory]
    [InlineData(null)]
    public void RemoveTag_Noop_IfTagsEmpty(string[] tags)
    {
        // Arrange
        var space = new Space{Tags = tags};
        
        // Arrange
        space.RemoveTag("foo");
        
        // Assert
        space.Tags.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTag_Noop_IfTagDoesntExist()
    {
        // Arrange
        var space = new Space {Tags = new[]{ "bar", "baz"}};
        
        // Arrange
        space.RemoveTag("foo");
        
        // Assert
        space.Tags.Should().BeEquivalentTo("bar", "baz");
    }

    [Fact]
    public void RemoveTag_Removes_IfTagExists()
    {
        // Arrange
        var space = new Space {Tags = new[]{ "bar", "baz"}};
        
        // Arrange
        space.RemoveTag("bar");
        
        // Assert
        space.Tags.Should().BeEquivalentTo("baz");
    }
}