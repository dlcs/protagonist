using DLCS.Model.IIIF;
using IIIF.ImageApi;

namespace DLCS.Model.Tests.IIIF;

public class IIIFXTests
{
    [Theory]
    [InlineData(null, null, 0)]
    [InlineData(100, null, 100)]
    [InlineData(null, 100, 100)]
    [InlineData(200, 100, 200)]
    public void GetMaxDimension_Correct(int? width, int? height, int expected)
    {
        var sizeParameter = new SizeParameter { Width = width, Height = height };
        sizeParameter.GetMaxDimension().Should().Be(expected);
    }
}