using System;
using System.Collections.Generic;

namespace DLCS.Core.Tests;

public class ChangeManagerTests
{
    [Fact]
    public void DefaultNullProperties_SetsDefaults()
    {
        // Arrange
        var defaults = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "default nullable string",
            NullableLongVal = 123,
            NullableDateTimeVal = DateTime.Today,
            NullableList = new List<string> { "foo", "bar" }
        };

        var toUpdate = new ChangeTest();
        
        var expected = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "default nullable string",
            NullableLongVal = 123,
            NullableDateTimeVal = DateTime.Today,
            NullableList = new List<string> { "foo", "bar" }
        };
        
        // Act
        toUpdate.DefaultNullProperties(defaults);
        
        // Assert
        toUpdate.Should().BeEquivalentTo(expected, opts => opts.Excluding(d => d.NullableList));
    }
    
    [Fact]
    public void DefaultNullProperties_IgnoresNonNullValues()
    {
        // Arrange
        var defaults = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "default nullable string",
            NullableLongVal = 123,
            NullableDateTimeVal = DateTime.Today,
            NullableList = new List<string> { "foo", "bar" },
        };

        var toUpdate = new ChangeTest
        {
            StringVal = "",
            NullableStringVal = "No Change",
            NullableLongVal = 999,
            NullableDateTimeVal = DateTime.Today.AddDays(2),
            NullableList = new List<string> { "baz" },
        };
        
        var expected = new ChangeTest
        {
            StringVal = "",
            NullableStringVal = "No Change",
            NullableLongVal = 999,
            NullableDateTimeVal = DateTime.Today.AddDays(2),
            NullableList = new List<string> { "baz" },
        };
        
        // Act
        toUpdate.DefaultNullProperties(defaults);
        
        // Assert
        toUpdate.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void DefaultNullProperties_HandlesMixOfNullAndNotNull()
    {
        // Arrange
        var defaults = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "default nullable string",
            NullableLongVal = 123,
            NullableDateTimeVal = DateTime.Today,
            NullableList = new List<string> { "baz" },
        };

        var toUpdate = new ChangeTest
        {
            StringVal = "",
            NullableStringVal = "default nullable string",
            NullableLongVal = 0,
        };
        
        var expected = new ChangeTest
        {
            StringVal = "",
            NullableStringVal = "default nullable string",
            NullableLongVal = 0,
            NullableDateTimeVal = DateTime.Today,
            NullableList = new List<string> { "baz" },
        };
        
        // Act
        toUpdate.DefaultNullProperties(defaults);
        
        // Assert
        toUpdate.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ApplyChanges_IgnoresDefaults()
    {
        // Arrange
        var existingObject = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "default nullable string",
            NullableLongVal = 123,
            NullableDateTimeVal = DateTime.Today
        };

        var candidateChanges = new ChangeTest();
        
        var expected = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "default nullable string",
            NullableLongVal = 123,
            NullableDateTimeVal = DateTime.Today
        };
        
        // Act
        var changeCount = existingObject.ApplyChanges(candidateChanges);
        
        // Assert
        existingObject.Should().BeEquivalentTo(expected);
        changeCount.Should().Be(0);
    }
    
    [Fact]
    public void ApplyChanges_IgnoresMatchingValues()
    {
        // Arrange
        var existingObject = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "default nullable string",
            NullableLongVal = 123,
            NullableDateTimeVal = DateTime.Today
        };

        var candidateChanges = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "default nullable string",
            NullableLongVal = 123,
            NullableDateTimeVal = DateTime.Today
        };
        
        var expected = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "default nullable string",
            NullableLongVal = 123,
            NullableDateTimeVal = DateTime.Today
        };
        
        // Act
        var changeCount = existingObject.ApplyChanges(candidateChanges);
        
        // Assert
        existingObject.Should().BeEquivalentTo(expected);
        changeCount.Should().Be(0);
    }
    
    [Fact]
    public void ApplyChanges_ReplacesChanges()
    {
        // Arrange
        var existingObject = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "default nullable string",
            NullableLongVal = null,
            NullableDateTimeVal = DateTime.Today.AddDays(2)
        };

        var candidateChanges = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "",
            NullableLongVal = 0,
            NullableDateTimeVal = DateTime.Today,
        };
        
        var expected = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "",
            NullableLongVal = 0,
            NullableDateTimeVal = DateTime.Today,
        };
        
        // Act
        var changeCount = existingObject.ApplyChanges(candidateChanges);
        
        // Assert
        existingObject.Should().BeEquivalentTo(expected);
        changeCount.Should().Be(3);
    }
    
    [Fact]
    public void ApplyChanges_RespectsIgnoreList()
    {
        // Arrange
        var existingObject = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "default nullable string",
            NullableLongVal = null,
            NullableDateTimeVal = DateTime.Today.AddDays(2),
            NumberVal = 2
        };

        var candidateChanges = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "",
            NullableLongVal = 0,
            NullableDateTimeVal = DateTime.Today,
            NumberVal = 100
        };
        
        var expected = new ChangeTest
        {
            StringVal = "default string",
            NullableStringVal = "",
            NullableLongVal = 0,
            NullableDateTimeVal = DateTime.Today.AddDays(2),
            NumberVal = 2
        };
        
        // Act
        var changeCount = existingObject.ApplyChanges(candidateChanges, i => i.NumberVal, i => i.NullableDateTimeVal);
        
        // Assert
        existingObject.Should().BeEquivalentTo(expected);
        changeCount.Should().Be(2, "4 values changed but 2 are ignored");
    }
}

public class ChangeTest
{
    public string StringVal { get; set; }
    
    public string? NullableStringVal { get; set; }
    
    public long? NullableLongVal { get; set; }
    
    public DateTime? NullableDateTimeVal { get; set; }
    
    public List<string> NullableList { get; set; }
    
    public int NumberVal { get; set; }

    public bool GetOnly { get; } = true;
}