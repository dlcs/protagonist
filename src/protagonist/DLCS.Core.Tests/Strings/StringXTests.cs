using System;
using DLCS.Core.Strings;

namespace DLCS.Core.Tests.Strings;

public class StringXTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void HasText_False_IfNullOrWhitespace(string str)
        => str.HasText().Should().BeFalse();
    
    [Fact]
    public void HasText_True_IfNotNullOrWhitespace()
        => "x".HasText().Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void DecodeBase64_ReturnsPassedString_IfNullOrWhitespace(string str)
    {
        var actual = str.DecodeBase64();

        actual.Should().Be(str);
    }

    [Fact]
    public void DecodeBase64_Throws_IfNotEncoded()
    {
        const string str = "foo bar baz";

        Action action = () => str.DecodeBase64();
        
        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void DecodeBase64_ReturnsDecodedString()
    {
        const string str = "Zm9vIGJhciBiYXo=";
        
        var actual = str.DecodeBase64();

        actual.Should().Be("foo bar baz");
    }

    [Theory]
    [InlineData("this is a test", "thisIsATest", false)]
    [InlineData("this is another test", "thisIsAnotherTest", false)]
    [InlineData("this is another test", "thisIsAnotherTest", true)]
    [InlineData(" ", "", false)]
    [InlineData("Start with Capital ", "startWithCapital", true)]
    [InlineData("Start with Capital ", "StartWithCapital", false)]
    public void ToCamelCase_Transforms(string from, string to, bool lower)
    {
        var actual = from.ToCamelCase(lower);

        actual.Should().Be(to);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ToConcatenated_ReturnsString_IfNullOrEmpty(string str)
        => str.ToConcatenated('-', "hi").Should().Be(str);

    [Theory]
    [InlineData("foo-")]
    [InlineData("foo")]
    public void ToConcatenated_ReturnsConcatenatedString_EndsWithSeparator(string str)
    {
        // Arrange
        const string expected = "foo-bar-baz";
        
        // Act
        var result = str.ToConcatenated('-', "bar", "baz");

        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void ToConcatenated_TrimsAllElements()
    {
        // Arrange
        const string expected = "foo-bar-baz";
        
        // Act
        var result = "foo".ToConcatenated('-', "-bar-", "-baz");

        // Assert
        result.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void SplitSeparatedString_ReturnsEmptyList_IfNullOrEmpty(string str)
        => str.SplitSeparatedString("|").Should().BeEmpty();

    [Fact]
    public void SplitSeparatedString_SplitsStringCorrectly()
    {
        // Arrange
        const string original = "foo|bar||baz";
        var expected = new[] {"foo", "bar", "baz"};
        
        // Act
        var actual = original.SplitSeparatedString("|");
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData("myfile.jpg", "jpg")]
    [InlineData("my.file.jpg", "jpg")]
    [InlineData("myfile", "")]
    public void EverythingAfterLast_ReturnsExpected(string str, string expected)
    {
        // Act
        var actual = str.EverythingAfterLast('.');
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
}