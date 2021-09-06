using System;
using System.IO;
using DLCS.Repository.Strategy;
using FluentAssertions;
using Test.Helpers;
using Xunit;

namespace DLCS.Repository.Tests.Strategy
{
    public class OriginResponseTests
    {
        [Fact]
        public void Ctor_Throws_IfStreamNull()
        {
            // Arrange
            Action action = () => new OriginResponse(null);
            
            // Assert
            action.Should()
                .Throw<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'stream')");
        }
        
        [Fact]
        public void Empty_SetsNullStreamAndIsEmpty()
        {
            // Arrange
            var empty = OriginResponse.Empty;
            
            // Assert
            empty.Stream.Should().BeSameAs(Stream.Null);
            empty.IsEmpty.Should().BeTrue();
        }
        
        [Fact]
        public void WithContentLength_Throws_IfStreamEmpty()
        {
            // Arrange
            var empty = OriginResponse.Empty;
            
            // Act
            Action action = () => empty.WithContentLength(100);
            
            // Assert
            action.Should().Throw<InvalidOperationException>();
        }
        
        [Fact]
        public void WithContentType_Throws_IfStreamEmpty()
        {
            // Arrange
            var empty = OriginResponse.Empty;
            
            // Act
            Action action = () => empty.WithContentType("application/json");
            
            // Assert
            action.Should().Throw<InvalidOperationException>();
        }
        
        [Theory]
        [InlineData(null)]
        [InlineData(-1)]
        [InlineData(0)]
        public void WithContentLength_DoesNotSetContentLength_IfNullOrLessThan1(long? contentLength)
        {
            // Arrange
            var response = new OriginResponse("foo".ToMemoryStream());
            
            // Act
            response.WithContentLength(contentLength);
            
            // Assert
            response.ContentLength.Should().BeNull();
        }
    }
}