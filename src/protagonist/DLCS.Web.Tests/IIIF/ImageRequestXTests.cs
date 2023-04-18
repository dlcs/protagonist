using DLCS.Web.IIIF;
using FluentAssertions;
using IIIF.ImageApi;
using Xunit;

namespace DLCS.Web.Tests.IIIF;

public class ImageRequestXTests
{
    [Theory]
    [InlineData("tif")]
    [InlineData("png")]
    [InlineData("gif")]
    [InlineData("jp2")]
    [InlineData("pdf")]
    [InlineData("webp")]
    public void IsCandidateForThumbHandling_False_IfNonJpgFormat(string format)
    {
        // Arrange
        var imageRequest = new ImageRequest
            { Format = format, Quality = "default", Rotation = new RotationParameter(), Size = new SizeParameter() };

        // Act
        var canHandle = imageRequest.IsCandidateForThumbHandling(out var message);

        // Assert
        canHandle.Should().BeFalse();
        message.Should().Be($"Requested format '{format}' not supported, use 'jpg'");
    }

    [Theory]
    [InlineData("gray")]
    [InlineData("bitonal")]
    public void IsCandidateForThumbHandling_False_IfNonDefaultQuality(string quality)
    {
        // Arrange
        var imageRequest = new ImageRequest
            { Format = "jpg", Quality = quality, Rotation = new RotationParameter(), Size = new SizeParameter() };

        // Act
        var canHandle = imageRequest.IsCandidateForThumbHandling(out var message);

        // Assert
        canHandle.Should().BeFalse();
        message.Should().Be($"Requested quality '{quality}' not supported, use 'default' or 'color'");
    }
    
    [Theory]
    [InlineData("90")]
    [InlineData("120")]
    [InlineData("!90")]
    [InlineData("!120")]
    [InlineData("!0")]
    public void IsCandidateForThumbHandling_False_IfNonZeroRotation(string rotation)
    {
        // Arrange
        var imageRequest = new ImageRequest
        {
            Format = "jpg", Quality = "default", Rotation = RotationParameter.Parse(rotation),
            Size = new SizeParameter()
        };

        // Act
        var canHandle = imageRequest.IsCandidateForThumbHandling(out var message);

        // Assert
        canHandle.Should().BeFalse();
        message.Should().Be("Requested rotation value not supported, use '0'");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("color")]
    public void IsCandidateForThumbHandling_True_IfJpg_Default_NoRotation_NotPctSize(string quality)
    {
        // Arrange
        var imageRequest = new ImageRequest
            { Format = "jpg", Quality = quality, Rotation = new RotationParameter(), Size = new SizeParameter() };

        // Act
        var canHandle = imageRequest.IsCandidateForThumbHandling(out var message);

        // Assert
        canHandle.Should().BeTrue();
        message.Should().BeNull();
    }
    
    [Fact]
    public void IsCandidateForThumbHandling_False_IfPercentSize()
    {
        // Arrange
        var imageRequest = new ImageRequest
        {
            Format = "jpg", Quality = "default", Rotation = new RotationParameter(),
            Size = SizeParameter.Parse("pct:24")
        };

        // Act
        var canHandle = imageRequest.IsCandidateForThumbHandling(out var message);

        // Assert
        canHandle.Should().BeFalse();
        message.Should().Be("Requested pct: size value not supported");
    }
}