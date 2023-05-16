using System;
using DLCS.Core.Guard;
using FluentAssertions;
using Xunit;

namespace DLCS.Core.Tests.Guard;

public class GuardXTests
{
    [Fact]
    public void ThrowIfNull_Throws_IfArgumentNull()
    {
        // Act
        Action action = () => GuardX.ThrowIfNull<object>(null, "foo");
        
        // Assert
        action.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'foo')");
    } 
    
    [Fact]
    public void ThrowIfNull_ReturnsProvidedValue_IfNotNull()
    {
        // Arrange
        object val = DateTime.Now;
        
        // Act
        var actual = val.ThrowIfNull(nameof(val));
        
        // Assert
        actual.Should().Be(val);
    }
    
    [Fact]
    public void ThrowIfNull_NullableStruct_Throws_IfArgumentNull()
    {
        // Act
        Action action = () => GuardX.ThrowIfNull<int?>(null, "foo");
        
        // Assert
        action.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'foo')");
    } 
    
    [Fact]
    public void ThrowIfNull_NullableStruct_ReturnsProvidedValue_IfNotNull()
    {
        // Arrange
        int? val = 12345;
        
        // Act
        var actual = val.ThrowIfNull(nameof(val));
        
        // Assert
        actual.Should().Be(val);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ThrowIfNullOrEmpty_Throws_IfNullOrEmpty(string val)
    {
        // Act
        Action action = () => GuardX.ThrowIfNullOrEmpty(val, "foo");

        // Assert
        action.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'foo')");
    }

    [Fact]
    public void ThrowIfNullOrEmpty_ReturnsProvidedString_IfNotNullOrEmpty()
    {
        // Arrange
        const string val = "foo bar";
        
        // Act
        var actual = val.ThrowIfNullOrEmpty(nameof(val));
        
        // Assert
        actual.Should().Be(val);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ThrowIfNullOrWhiteSpace_Throws_IfArgumentNullOrWhiteSpace(string str)
    {
        // Act
        Action action = () => str.ThrowIfNullOrWhiteSpace("foo");
        
        // Assert
        action.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'foo')");
    }
    
    [Fact]
    public void ThrowIfNullOrWhiteSpace_ReturnsProvidedString_IfNotNull()
    {
        // Arrange
        const string val = "hi";

        // Act
        var actual = val.ThrowIfNull(nameof(val));
        
        // Assert
        actual.Should().Be(val);
    }
}