using System;
using DLCS.Core.Guard;
using FluentAssertions;
using Xunit;

namespace DLCS.Core.Tests.Guard
{
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
    }
}