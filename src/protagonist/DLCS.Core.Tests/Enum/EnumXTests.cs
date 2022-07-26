using System;
using System.ComponentModel;
using DLCS.Core.Enum;
using FluentAssertions;
using Xunit;

namespace DLCS.Core.Tests.Enum
{
    public class EnumXTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void GetEnumFromString_ThrowsIfNullOrEmptyValuePassed(string lookingFor)
        {
            // Act
            Action action = () => lookingFor.GetEnumFromString<TestEnum>();
            
            // Assert
            action.Should().Throw<ArgumentNullException>();
        }
        
        [Theory]
        [InlineData("BrownBear", TestEnum.BrownBear, "With description")]
        [InlineData("BlackBear", TestEnum.BlackBear, "No description")]
        public void GetEnumFromString_ReturnsValue_IfMatchesEnum(string lookingFor, TestEnum expected, string reason)
        {
            // Act
            var result = lookingFor.GetEnumFromString<TestEnum>();
            
            // Assert
            result.Should().Be(expected, reason);
        }
        
        [Fact]
        public void GetEnumFromString_ReturnsValue_IfMatchesDescription()
        {
            // Arrange
            const string lookingFor = "Brown Bear";
            
            // Act
            var result = lookingFor.GetEnumFromString<TestEnum>();
            
            // Assert
            result.Should().Be(TestEnum.BrownBear);
        }
        
        [Fact]
        public void GetEnumFromString_ReturnsDefault_IfNoMatch()
        {
            // Arrange
            const string lookingFor = "Panda Bear";
            
            // Act
            var result = lookingFor.GetEnumFromString<TestEnum>();
            
            // Assert
            result.Should().Be(TestEnum.Unknown);
        }
        
        [Fact]
        public void GetEnumFromString_Throws_IfNoMatch_AndDefaultIfNotFoundFalse()
        {
            // Arrange
            const string lookingFor = "Panda Bear";
            
            // Act
            Action action = () => lookingFor.GetEnumFromString<TestEnum>(false);
            
            // Assert
            action.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void GetDescription_ReturnsDescription_IfFound()
        {
            // Arrange
            const string expected = "Brown Bear";
            
            // Act
            var actual = TestEnum.BrownBear.GetDescription();
            
            // Assert
            actual.Should().Be(expected);
        }
        
        [Fact]
        public void GetDescription_FallsbackToString_IfNoDescription()
        {
            // Arrange
            const string expected = "BlackBear";
            
            // Act
            var actual = TestEnum.BlackBear.GetDescription();
            
            // Assert
            actual.Should().Be(expected);
        }
        
        public enum TestEnum
        {
            Unknown, 
            
            BlackBear = 1,
            
            [Description("Brown Bear")]
            BrownBear = 2,
            
            [Description("BrownBear")]
            BrownBearFromDescription = 3
        }
    }
}