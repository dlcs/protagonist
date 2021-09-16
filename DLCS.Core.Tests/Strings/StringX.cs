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
    }
}