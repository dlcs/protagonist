using System.IO;
using DLCS.Core.Streams;
using FluentAssertions;
using Xunit;

namespace DLCS.Core.Tests.Guard;

public class StreamXTests
{
    [Fact]
    public void IsNull_True_IfStreamIsNullObject()
    {
        // Arrange
        Stream stream = null;

        // Assert
        stream.IsNull().Should().BeTrue();
    }
    
    [Fact]
    public void IsNull_True_IfStreamIsNull()
    {
        // Arrange
        Stream stream = Stream.Null;

        // Assert
        stream.IsNull().Should().BeTrue();
    }
    
    [Fact]
    public void IsNull_False_IfStreamIsNull()
    {
        // Arrange
        Stream stream = new MemoryStream();

        // Assert
        stream.IsNull().Should().BeFalse();
    }
}