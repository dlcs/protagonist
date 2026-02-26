using DLCS.Web.IIIF;
using IIIF.ImageApi;

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

    [Fact]
    public void GetImageRequestOnly_Incorrect_IfInfoJson()
    {
        // This is obviously not a great behaviour but documents the lack of support for info.json etc
        var imageRequest = ImageRequest.Parse("image/info.json", "");
        imageRequest.GetImageRequestOnly().Should().Be("///.", "There are no safety checks");
    }

    [Fact]
    public void GetImageRequestOnly_Correct_AfterParse()
    {
        var imageRequest = ImageRequest.Parse("iiif-img/27/1/my-asset/full/800,/0/default.jpg", "iiif-img/27/1/");
        imageRequest.GetImageRequestOnly().Should().Be("full/800,/0/default.jpg");
    }

    [Fact]
    public void GetImageRequestOnly_Correct_AfterAlteringParsedObject()
    {
        var imageRequest = ImageRequest.Parse("iiif-img/27/1/my-asset/full/800,/0/default.jpg", "iiif-img/27/1/");

        imageRequest.Size = SizeParameter.Parse("pct:24");
        imageRequest.Quality = "bitonal";
        imageRequest.Format = "tif";
        imageRequest.Rotation = new RotationParameter { Angle = 90, Mirror = true };
        imageRequest.Region = new RegionParameter { Square = true };
        imageRequest.GetImageRequestOnly().Should().Be("square/pct:24/!90/bitonal.tif",
            "Value reflects current state of ImageRequest");
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("tif")]
    [InlineData("gif")]
    [InlineData("png")]
    public void IsCandidateForImageHandling_True_IfAcceptedFormat(string format)
    {
        var imageRequest = new ImageRequest
            { Format = format, Quality = "default", Size = new SizeParameter { Max = true } };
        imageRequest.IsCandidateForImageHandling(out var message).Should().BeTrue($"{format} accepted");
        message.Should().BeNull();
    }

    [Theory]
    [InlineData("webp")]
    [InlineData("jp2")]
    [InlineData("pdf")]
    [InlineData("not-in-spec")]
    public void IsCandidateForImageHandling_False_IfNotAcceptedFormat(string format)
    {
        var imageRequest = new ImageRequest
            { Format = format, Quality = "default", Size = new SizeParameter { Max = true } };
        imageRequest.IsCandidateForImageHandling(out var message).Should().BeFalse($"{format} not accepted");
        message.Should().Match($"Requested format '{format}' not supported, must be one of '*'");
    }

    [Theory]
    [InlineData("default")]
    [InlineData("gray")]
    [InlineData("bitonal")]
    [InlineData("color")]
    public void IsCandidateForImageHandling_True_IfAcceptedQuality(string quality)
    {
        var imageRequest = new ImageRequest
            { Format = "jpg", Quality = quality, Size = new SizeParameter { Max = true } };
        imageRequest.IsCandidateForImageHandling(out var message).Should().BeTrue($"{quality} accepted");
        message.Should().BeNull();
    }

    [Fact]
    public void IsCandidateForImageHandling_False_IfNotAcceptedQuality()
    {
        var imageRequest = new ImageRequest
            { Format = "jpg", Quality = "translucent", Size = new SizeParameter { Max = true } };
        imageRequest.IsCandidateForImageHandling(out var message).Should().BeFalse();
        message.Should().Match("Requested format 'translucent' not supported, must be one of '*'");
    }
    
    [Theory]
    [InlineData("max")]
    [InlineData("^max")]
    [InlineData("full")]
    [InlineData("!10,10")]
    [InlineData("^!10,10")]
    [InlineData("10,10")]
    [InlineData("^10,10")]
    [InlineData("20,")]
    [InlineData("^20,")]
    [InlineData(",20")]
    [InlineData("^,20")]
    public void IsCandidateForImageHandling_True_IfSizeValid(string size)
    {
        var imageRequest = new ImageRequest { Format = "jpg", Quality = "default", Size = SizeParameter.Parse(size) };
        imageRequest.IsCandidateForImageHandling(out var message).Should().BeTrue();
        message.Should().BeNull();
    }

    [Theory]
    [InlineData("0,")]
    [InlineData(",0")]
    [InlineData("!0,0")]
    [InlineData("20,0")]
    [InlineData("0,20")]
    public void IsCandidateForImageHandling_False_IfZeroSize(string size)
    {
        var imageRequest = new ImageRequest { Format = "jpg", Quality = "default", Size = SizeParameter.Parse(size) };
        imageRequest.IsCandidateForImageHandling(out var message).Should().BeFalse();
        message.Should().Be("Requested size must be greater than 0");
    }
}
