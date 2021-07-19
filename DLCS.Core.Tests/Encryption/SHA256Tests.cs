using System;
using DLCS.Core.Encryption;
using FluentAssertions;
using Xunit;

namespace DLCS.Core.Tests.Encryption
{
    public class SHA256Tests
    {
        private readonly SHA256 sut;

        public SHA256Tests()
        {
            sut = new SHA256();
        }
        
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Encrypt_Throws_IfSourceNullOrWhitespace(string source)
        {
            // Act
            Action action = () => sut.Encrypt(source);
            
            // Assert
            action.Should()
                .Throw<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'source')");
        }

        [Fact]
        public void Encrypt_IsDeterministic()
        {
            // Arrange
            const string origin = "foo-bar-baz";
            
            // Act
            var first = sut.Encrypt(origin);
            var second = sut.Encrypt(origin);
            
            // Assert
            first.Should().Be(second);
        }
    }
}