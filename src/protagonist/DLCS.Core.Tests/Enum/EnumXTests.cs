using System;
using System.Collections.Generic;
using System.ComponentModel;
using DLCS.Core.Enum;

namespace DLCS.Core.Tests.Enum;

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

    [Theory]
    [InlineData("Brown Bear", true)]
    [InlineData("BrownBear", true)]
    [InlineData("Polar Bear", false)]
    public void IsValidEnumValue_ReturnsCorrectValue(string lookingFor, bool expected)
    {
        // Act
        var result = lookingFor.IsValidEnumValue<TestEnum>();
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void IsValidEnumValue_ReturnsFalse_IfNullOrEmptyValuePassed(string lookingFor)
    {
        // Act
        var result = lookingFor.IsValidEnumValue<TestEnum>();
        
        // Assert
        result.Should().Be(false);
    }
    
    [Fact]
    public void ToEnumFlags_ConvertsListToFlags_WhenCalledCorrectly()
    {
        // Arrange
        var enumList = new List<string>()
        {
            "BlackBear",
            "BrownBearDesc"
        };
        
        // Act
        var result = enumList.ToEnumFlags<TestFlagEnum>();
        
        // Assert
        result.HasFlag(TestFlagEnum.BlackBear).Should().BeTrue();
        result.HasFlag(TestFlagEnum.BrownBear).Should().BeFalse();
        result.HasFlag(TestFlagEnum.PolarBear).Should().BeFalse();
        result.HasFlag(TestFlagEnum.BrownBearFromDescription).Should().BeTrue();
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
    
    [Flags]
    public enum TestFlagEnum
    {
        BlackBear = 0,
        
        BrownBear = 1,
        
        [Description("BrownBearDesc")]
        BrownBearFromDescription = 2,
        
        PolarBear = 4
    }
}