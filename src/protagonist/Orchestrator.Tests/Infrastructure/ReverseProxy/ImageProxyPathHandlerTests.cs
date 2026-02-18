using System.Net;
using IIIF;
using IIIF.ImageApi;
using Orchestrator.Infrastructure.ReverseProxy;

namespace Orchestrator.Tests.Infrastructure.ReverseProxy;

public class ImageProxyPathHandlerTests
{
    [Theory]
    [InlineData("full/^full")]
    [InlineData("square/^full")]
    [InlineData("0,0,512,512/^full")]
    [InlineData("pct:41.6,7.5,40,70/^full")]
    public void GetProxyImageRequest_Invalid_UpscaleFull(string imageRequest)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.jpg", "");
        var result = parsed.GetProxyImageRequest(new Size(100, 100), 500);
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("'^full' size is invalid. Use 'full' or '^max' instead.");
        result.ErrorStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("full/101,")]
    [InlineData("square/,101")]
    [InlineData("0,0,512,512/101,101")]
    [InlineData("pct:41.6,7.5,40,70/!101,101")]
    public void GetProxyImageRequest_Invalid_WouldRequireUpscale(string imageRequest)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.jpg", "");
        var result = parsed.GetProxyImageRequest(new Size(100, 100), 500);
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().MatchEquivalentOf("SizeParameter * cannot upscale image size '*'");
        result.ErrorStatusCode.Should().Be(HttpStatusCode.NotImplemented);
    }
    
    [Theory]
    [InlineData("101,0,10,10/full")]
    [InlineData("101,101,10,10/256,")]
    [InlineData("10,101,10,10/10,10")]
    [InlineData("pct:101,0,10,10/full")]
    [InlineData("pct:101,101,10,10/!10,10")]
    [InlineData("pct:10,101,10,10/full")]
    public void GetProxyImageRequest_Invalid_RegionOutOfBounds(string imageRequest)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        var result = parsed.GetProxyImageRequest(new Size(100, 100), 500);
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Region is outside image bounds");
        result.ErrorStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("0,0,512,512/11,", 100, 100, 10)]
    [InlineData("0,0,512,512/11,11", 100, 100, 10)]
    [InlineData("0,0,512,512/,11", 100, 100, 10)]
    [InlineData("full/11,", 100, 100, 10)]
    [InlineData("full/11,11", 100, 100, 10)]
    [InlineData("full/,11", 100, 100, 10)]
    [InlineData("full/pct:15", 100, 100, 10)]
    public void GetProxyImageRequest_Invalid_TooLarge(string imageRequest, int w, int h, int maxWidth)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        var result = parsed.GetProxyImageRequest(new Size(w, h), maxWidth);
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().MatchEquivalentOf("Requested size '*' exceeds maxWidth of *");
        result.ErrorStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(ValidSizeData.Portrait), MemberType = typeof(ValidSizeData))]
    public void GetProxyImageRequest_Valid_Portrait(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        var result = parsed.GetProxyImageRequest(imageSize, 5000);
        result.IsValid.Should().BeTrue(imageRequest);
        result.ErrorMessage.Should().BeNull(imageRequest);
        result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
        result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
    }
    
    [Theory]
    [MemberData(nameof(ValidSizeData.PortraitRestrictedMaxWidth), MemberType = typeof(ValidSizeData))]
    public void GetProxyImageRequest_Valid_Portrait_Restricted(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        var result = parsed.GetProxyImageRequest(imageSize, 500);
        result.IsValid.Should().BeTrue(imageRequest);
        result.ErrorMessage.Should().BeNull(imageRequest);
        result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
        result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
    }
    
    [Theory]
    [MemberData(nameof(ValidSizeData.Landscape), MemberType = typeof(ValidSizeData))]
    public void GetProxyImageRequest_Valid_Landscape(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/180/bitonal.jpg", "");
        var result = parsed.GetProxyImageRequest(imageSize, 5000);
        result.IsValid.Should().BeTrue(imageRequest);
        result.ErrorMessage.Should().BeNull(imageRequest);
        result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
        result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
    }
    
    [Theory]
    [MemberData(nameof(ValidSizeData.LandscapeRestrictedMaxWidth), MemberType = typeof(ValidSizeData))]
    public void GetProxyImageRequest_Valid_Landscape_Restricted(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        var result = parsed.GetProxyImageRequest(imageSize, 500);
        result.IsValid.Should().BeTrue(imageRequest);
        result.ErrorMessage.Should().BeNull(imageRequest);
        result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
        result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
    }
    
    /// <summary>
    /// Values are: imageRequest, imageSize, expectedSize, expectedProxySize
    /// </summary>
    private class ValidSizeData
    {
        private static readonly Size PortraitSize = new(1000, 2000);
        private static readonly Size LandscapeSize = new(2000, 1000);

        /// <summary>
        /// A series of valid image requests and expected sizes for Portrait images. maxWidth is 5000.
        /// </summary>
        public static TheoryData<string, Size, Size, string> Portrait =>
            new()
            {
                { "100,100,512,512/512,", PortraitSize, Size.Square(512), "512," },
                { "0,0,512,512/,256", PortraitSize, Size.Square(256), ",256" },
                { "0,0,128,512/64,256", PortraitSize, new Size(64, 256), "64,256" },
                { "0,0,512,256/!256,256", PortraitSize, new Size(256, 128), "!256,256" },
                { "0,0,512,256/max", PortraitSize, new Size(512, 256), "512,256" },
                { "0,0,512,256/full", PortraitSize, new Size(512, 256), "512,256" },
                { "0,0,512,256/^max", PortraitSize, new Size(5000, 2500), "^5000,2500" },
                { "pct:10,10,25,50/,256", PortraitSize, new Size(64, 256), ",256" },
                { "pct:10,10,25,50/^max", PortraitSize, new Size(1250, 5000), "^1250,5000" },
                { "square/512,256", PortraitSize, new Size(512, 256), "512,256" },
                { "square/!256,512", PortraitSize, Size.Square(256), "!256,512" },
                { "square/full", PortraitSize, Size.Square(1000), "1000,1000" },
                { "square/pct:50", PortraitSize, Size.Square(500), "pct:50" },
                { "square/^pct:250", PortraitSize, Size.Square(2500), "^pct:250" },
                { "square/max", PortraitSize, Size.Square(1000), "1000,1000" },
                { "square/^max", PortraitSize, Size.Square(5000), "^5000,5000" },
                { "full/512,256", PortraitSize, new Size(512, 256), "512,256" },
                { "full/full", PortraitSize, new Size(1000, 2000), "1000,2000" },
                { "full/max", PortraitSize, new Size(1000, 2000), "1000,2000" },
                { "full/^max", PortraitSize, new Size(2500, 5000), "^2500,5000" },
            };
        
        /// <summary>
        /// A series of valid image requests and expected sizes for Landscape images. maxWidth is 5000.
        /// </summary>
        public static TheoryData<string, Size, Size, string> Landscape =>
            new()
            {
                { "100,100,512,512/512,", LandscapeSize, Size.Square(512), "512," },
                { "0,0,512,512/,256", LandscapeSize, Size.Square(256), ",256" },
                { "0,0,128,512/64,256", LandscapeSize, new Size(64, 256), "64,256" },
                { "0,0,512,256/!256,256", LandscapeSize, new Size(256, 128), "!256,256" },
                { "0,0,512,256/max", LandscapeSize, new Size(512, 256), "512,256" },
                { "0,0,512,256/full", LandscapeSize, new Size(512, 256), "512,256" },
                { "0,0,512,256/^max", LandscapeSize, new Size(5000, 2500), "^5000,2500" },
                { "pct:10,10,25,50/,256", LandscapeSize, new Size(256, 256), ",256" },
                { "pct:10,10,25,50/^max", LandscapeSize, new Size(5000, 5000), "^5000,5000" },
                { "square/512,256", LandscapeSize, new Size(512, 256), "512,256" },
                { "square/!256,512", LandscapeSize, Size.Square(256), "!256,512" },
                { "square/full", LandscapeSize, Size.Square(1000), "1000,1000" },
                { "square/pct:50", LandscapeSize, Size.Square(500), "pct:50" },
                { "square/^pct:250", LandscapeSize, Size.Square(2500), "^pct:250" },
                { "square/max", LandscapeSize, Size.Square(1000), "1000,1000" },
                { "square/^max", LandscapeSize, Size.Square(5000), "^5000,5000" },
                { "full/512,256", LandscapeSize, new Size(512, 256), "512,256" },
                { "full/full", LandscapeSize, new Size(2000, 1000), "2000,1000" },
                { "full/max", LandscapeSize, new Size(2000, 1000), "2000,1000" },
                { "full/^max", LandscapeSize, new Size(5000, 2500), "^5000,2500" },
            };
        
        /// <summary>
        /// A series of valid image requests and expected sizes for Portrait images. maxWidth is 500.
        /// </summary>
        public static TheoryData<string, Size, Size, string> PortraitRestrictedMaxWidth =>
            new()
        {
            { "0,0,512,256/max", PortraitSize, new Size(500, 250), "500,250" },
            { "0,0,512,256/full", PortraitSize, new Size(500, 250), "500,250" },
            { "0,0,512,256/^max", PortraitSize, new Size(500, 250), "500,250" },
            { "pct:10,10,25,50/^max", PortraitSize, new Size(125, 500), "125,500" },
            { "square/full", PortraitSize, Size.Square(500), "500,500" },
            { "square/max", PortraitSize, Size.Square(500), "500,500" },
            { "square/^max", PortraitSize, Size.Square(500), "500,500" },
            { "full/full", PortraitSize, new Size(250, 500), "250,500" },
            { "full/max", PortraitSize, new Size(250, 500), "250,500" },
            { "full/^max", PortraitSize, new Size(250, 500), "250,500" },
        };
        
        /// <summary>
        /// A series of valid image requests and expected sizes for Landscape images. maxWidth is 500.
        /// </summary>
        public static TheoryData<string, Size, Size, string> LandscapeRestrictedMaxWidth =>
            new()
            {
                { "0,0,512,256/max", LandscapeSize, new Size(500, 250), "500,250" },
                { "0,0,512,256/full", LandscapeSize, new Size(500, 250), "500,250" },
                { "0,0,512,256/^max", LandscapeSize, new Size(500, 250), "500,250" },
                { "pct:10,10,25,50/^max", LandscapeSize, new Size(500, 500), "500,500" },
                { "square/full", LandscapeSize, Size.Square(500), "500,500" },
                { "square/max", LandscapeSize, Size.Square(500), "500,500" },
                { "square/^max", LandscapeSize, Size.Square(500), "500,500" },
                { "full/full", LandscapeSize, new Size(500, 250), "500,250" },
                { "full/max", LandscapeSize, new Size(500, 250), "500,250" },
                { "full/^max", LandscapeSize, new Size(500, 250), "500,250" },
            };
    }
}
