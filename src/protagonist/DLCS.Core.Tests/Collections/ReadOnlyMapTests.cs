using System;
using System.Collections.Generic;
using DLCS.Core.Collections;
using FluentAssertions;
using Xunit;

namespace DLCS.Core.Tests.Collections;

public class ReadOnlyMapTests
{
    [Fact]
    public void Ctor_WithDictionary_SetsBothDirections()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> {["One"] = 1};
            
        // Act
        var map = new ReadOnlyMap<string, int>(dictionary);

        // Assert
        map.Forward["One"].Should().Be(1);
        map.Reverse[1].Should().Be("One");
    }

    [Fact]
    public void Ctor_Throws_IfNonUniqueValues_AndIgnoreDuplicateValuesFalse()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> {["One"] = 1, ["AlsoOne"] = 1};
            
        // Act + Assert
        Func<ReadOnlyMap<string, int>> call = () => new ReadOnlyMap<string, int>(dictionary);
        call.Should().Throw<ArgumentException>()
            .WithMessage("An item with the same key has already been added. Key: 1");
    }
        
    [Fact]
    public void Ctor_HandlesNonUniqueValues_IfIgnoreDuplicateValuesTrue()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> {["One"] = 1, ["AlsoOne"] = 1};
            
        // Act
        var map = new ReadOnlyMap<string, int>(dictionary, true);
            
        // Assert
        map.Reverse[1].Should().Be("One");
    }

    [Fact]
    public void Index_Throws_IfNotFound()
    {
        // Arrange
        var dictionary = new Dictionary<string, int> {["One"] = 1};
            
        // Act
        var map = new ReadOnlyMap<string, int>(dictionary);

        Func<int> action = () => map.Forward["Two"];
        action.Should().Throw<KeyNotFoundException>()
            .WithMessage("The given key 'Two' was not present in the dictionary.");
    }
}