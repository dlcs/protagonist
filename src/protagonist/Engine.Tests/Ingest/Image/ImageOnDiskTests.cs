using Engine.Ingest.Image;

namespace Engine.Tests.Ingest.Image;

public class ImageOnDiskTests
{
    [Theory]
    [InlineData(100, 200, 200)]
    [InlineData(200, 200, 200)]
    [InlineData(200, 100, 200)]
    public void MaxDimension_ReturnsMaxDimension(int w, int h, int expected) =>
        new ImageOnDisk { Width = w, Height = h }.MaxDimension.Should().Be(expected);
}
