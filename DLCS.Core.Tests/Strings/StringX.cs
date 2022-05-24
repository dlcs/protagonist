using System;
using DLCS.Core.Strings;
using FluentAssertions;
using Xunit;

namespace DLCS.Core.Tests.Strings
{
    public class StringX
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
        
        
        
    }
}