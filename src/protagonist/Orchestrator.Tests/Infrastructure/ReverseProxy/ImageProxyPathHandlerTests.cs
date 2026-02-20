using System.Net;
using DLCS.Model.IIIF;
using IIIF;
using IIIF.ImageApi;
using Orchestrator.Infrastructure.ReverseProxy;

namespace Orchestrator.Tests.Infrastructure.ReverseProxy;

public class ImageProxyPathHandlerTests
{
    // Some of the test payload/expectations are duplicated between V2 + V3 tests but I've kept separate for ease
    #region Image V2

    [Theory]
    [InlineData("full/^full")]
    [InlineData("square/^!101,101")]
    [InlineData("full/^max")]
    [InlineData("0,0,512,512/^,123")]
    [InlineData("pct:41.6,7.5,40,70/^pct:10")]
    public void GetProxyImageRequest_V2_Invalid(string imageRequest)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.jpg", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V2, new Size(100, 100), 500, strictMode);
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Be("Invalid size. '^' invalid for IIIF ImageApi 2.1");
            result.ErrorStatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
    
    [Theory]
    [MemberData(nameof(SizeData.RequireUpscale), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V2_Valid_WouldRequireUpscale(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        // Asserts we pass V2 parameters unchanged, even though dimension is larger than required 
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.jpg", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V2, imageSize, 500, false);
            result.IsValid.Should().BeTrue(imageRequest);
            result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
            result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
        }
    }
    
    [Theory]
    [MemberData(nameof(SizeData.RegionOutOfBounds), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V2_Invalid_RegionOutOfBounds(string imageRequest)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V2, Size.Square(100), 500, strictMode);
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Be("Region is outside image bounds");
            result.ErrorStatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
    
    [Theory]
    [MemberData(nameof(SizeData.ExceedMaxWidth), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V2_Invalid_ExceedMaxWidth(string imageRequest)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V2, Size.Square(100), 10, strictMode);
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Match("Requested size '*' exceeds maxWidth of *");
            result.ErrorStatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
    
    [Theory]
    [MemberData(nameof(SizeData.PortraitShared), MemberType = typeof(SizeData))]
    [MemberData(nameof(SizeData.PortraitV2), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V2_Valid_Portrait(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V2, imageSize, 5000, strictMode);
            result.IsValid.Should().BeTrue(imageRequest);
            result.ErrorMessage.Should().BeNull(imageRequest);
            result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
            result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
        }
    }
    
    [Theory]
    [MemberData(nameof(SizeData.PortraitRestrictedMaxWidth), MemberType = typeof(SizeData))]
    [MemberData(nameof(SizeData.PortraitRestrictedMaxWidthV2), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V2_Valid_Portrait_Restricted(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V2, imageSize, 500, strictMode);
            result.IsValid.Should().BeTrue(imageRequest);
            result.ErrorMessage.Should().BeNull(imageRequest);
            result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
            result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
        }
    }
    
    [Theory]
    [MemberData(nameof(SizeData.LandscapeShared), MemberType = typeof(SizeData))]
    [MemberData(nameof(SizeData.LandscapeV2), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V2_Valid_Landscape(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/180/bitonal.jpg", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V2, imageSize, 5000, strictMode);
            result.IsValid.Should().BeTrue(imageRequest);
            result.ErrorMessage.Should().BeNull(imageRequest);
            result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
            result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
        }
    }
    
    [Theory]
    [MemberData(nameof(SizeData.LandscapeRestrictedMaxWidth), MemberType = typeof(SizeData))]
    [MemberData(nameof(SizeData.LandscapeRestrictedMaxWidthV2), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V2_Valid_Landscape_Restricted(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V2, imageSize, 500, strictMode);
            result.IsValid.Should().BeTrue(imageRequest);
            result.ErrorMessage.Should().BeNull(imageRequest);
            result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
            result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
        }
    }

    #endregion
    
    #region Image V3
    [Theory]
    [InlineData("full/^full")]
    [InlineData("pct:0,0,10,50/^full")]
    public void GetProxyImageRequest_V3_InvalidUpscaleFull(string imageRequest)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.jpg", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V3, new Size(100, 100), 500, strictMode);
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Be("Invalid size. 'full' invalid for IIIF ImageApi 3.0");
            result.ErrorStatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    [Theory]
    [InlineData("square/full")]
    [InlineData("512,512,1024,1024/full")]
    public void GetProxyImageRequest_V3_InvalidFull_IfStrictMode(string imageRequest)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.jpg", "");
        var result = parsed.GetProxyImageRequest(Version.V3, new Size(100, 100), 500);
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid size. 'full' invalid for IIIF ImageApi 3.0");
        result.ErrorStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [MemberData(nameof(SizeData.LandscapeV2), MemberType = typeof(SizeData))]
    [MemberData(nameof(SizeData.PortraitV2), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V3_TreatsFullAsMax_IfLaxMode(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.jpg", "");
        
        // Slightly hacky but we have examples of /full/ in v2 payloads so use those for testing
        if (!parsed.IsExplicitFullSize()) return;
        var result = parsed.GetProxyImageRequest(Version.V3, imageSize, 5000, false);
        result.IsValid.Should().BeTrue(imageRequest);
        result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
        result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
    }

    [Theory]
    [MemberData(nameof(SizeData.RequireUpscale), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V3_Invalid_WouldRequireUpscale_NoCaret(string imageRequest, Size imageSize, Size _,
        string __)
    {
        // Asserts we pass V3 parameters unchanged, even though dimension is larger than required 
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.jpg", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V3, imageSize, 500, strictMode);
            result.IsValid.Should().BeFalse(imageRequest);
            result.ErrorMessage.Should().Match("SizeParameter /*/ cannot upscale image size '*'", imageRequest);
            result.ErrorStatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
    
    [Theory]
    [MemberData(nameof(SizeData.RegionOutOfBounds), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V3_Invalid_RegionOutOfBounds(string imageRequest)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V3, Size.Square(100), 500, strictMode);
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Be("Region is outside image bounds");
            result.ErrorStatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
    
    [Theory]
    [MemberData(nameof(SizeData.ExceedMaxWidth), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V3_Invalid_ExceedMaxWidth(string imageRequest)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V3, Size.Square(100), 10, strictMode);
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Match("Requested size '*' exceeds maxWidth of *");
            result.ErrorStatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
    
    [Theory]
    [MemberData(nameof(SizeData.PortraitShared), MemberType = typeof(SizeData))]
    [MemberData(nameof(SizeData.PortraitV3), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V3_Valid_Portrait(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V3, imageSize, 5000, strictMode);
            result.IsValid.Should().BeTrue(imageRequest);
            result.ErrorMessage.Should().BeNull(imageRequest);
            result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
            result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
        }
    }
    
    [Theory]
    [MemberData(nameof(SizeData.PortraitRestrictedMaxWidth), MemberType = typeof(SizeData))]
    [MemberData(nameof(SizeData.PortraitRestrictedMaxWidthV3), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V3_Valid_Portrait_Restricted(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V3, imageSize, 500, strictMode);
            result.IsValid.Should().BeTrue(imageRequest);
            result.ErrorMessage.Should().BeNull(imageRequest);
            result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
            result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
        }
    }
    
    [Theory]
    [MemberData(nameof(SizeData.LandscapeShared), MemberType = typeof(SizeData))]
    [MemberData(nameof(SizeData.LandscapeV3), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V3_Valid_Landscape(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/180/bitonal.jpg", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V3, imageSize, 5000, strictMode);
            result.IsValid.Should().BeTrue(imageRequest);
            result.ErrorMessage.Should().BeNull(imageRequest);
            result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
            result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
        }
    }
    
    [Theory]
    [MemberData(nameof(SizeData.LandscapeRestrictedMaxWidth), MemberType = typeof(SizeData))]
    [MemberData(nameof(SizeData.LandscapeRestrictedMaxWidthV3), MemberType = typeof(SizeData))]
    public void GetProxyImageRequest_V3_Valid_Landscape_Restricted(string imageRequest, Size imageSize, Size expectedSize,
        string sizeParameter)
    {
        var parsed = ImageRequest.Parse($"asset/{imageRequest}/0/default.tif", "");
        foreach (var strictMode in new[] { true, false })
        {
            var result = parsed.GetProxyImageRequest(Version.V3, imageSize, 500, strictMode);
            result.IsValid.Should().BeTrue(imageRequest);
            result.ErrorMessage.Should().BeNull(imageRequest);
            result.RequestedSize.Should().BeEquivalentTo(expectedSize, imageRequest);
            result.ProxySizeParameter!.ToString().Should().Be(sizeParameter, imageRequest);
        }
    }
    #endregion
    
    private class SizeData
    {
        private static readonly Size PortraitSize = new(1000, 2000);
        private static readonly Size LandscapeSize = new(2000, 1000);

        /// <summary>
        /// A series of valid image requests where region is out of bounds for 100,100 image
        /// </summary>
        public static TheoryData<string> RegionOutOfBounds =>
        [
            "101,0,10,10/max",
            "101,101,10,10/256,",
            "10,101,10,10/10,10",
            "pct:101,0,10,10/max",
            "pct:101,101,10,10/!10,10",
            "pct:10,101,10,10/max"
        ];
        
        /// <summary>
        /// A series of valid image requests where size exceeds maxWidth of 10 for 100,100 image
        /// </summary>
        public static TheoryData<string> ExceedMaxWidth =>
        [
            "0,0,512,512/11,",
            "0,0,512,512/11,11",
            "0,0,512,512/,11",
            "full/11,",
            "full/11,11",
            "full/,11",
            "full/pct:15",
            "square/11,",
            "square/11,11",
            "square/,11",
        ];

        /// <summary>
        /// A series of valid image requests that would result in an upscaled image, without ^
        ///
        /// Values are: imageRequest, imageSize, expectedSize, expectedProxySize
        /// </summary>
        /// <remarks>These contain expected size etc but may result in an error, depending on version</remarks>
        public static TheoryData<string, Size, Size, string> RequireUpscale =>
            new()
            {
                { "full/101,", Size.Square(100), Size.Square(101), "101," },
                { "square/,101,", Size.Square(100), Size.Square(101), ",101" },
                { "0,0,512,512/101,101", Size.Square(100), Size.Square(101), "101,101" },
                { "pct:41.6,7.5,40,70/!101,101", Size.Square(100), new Size(58, 101), "!101,101" },
            };
        
        /// <summary>
        /// A series of valid image requests and expected sizes for Portrait images. maxWidth is 5000
        ///
        /// Values are: imageRequest, imageSize, expectedSize, expectedProxySize
        /// </summary>
        public static TheoryData<string, Size, Size, string> PortraitShared =>
            new()
            {
                { "100,100,512,512/512,", PortraitSize, Size.Square(512), "512," },
                { "0,0,512,512/,256", PortraitSize, Size.Square(256), ",256" },
                { "0,0,128,512/64,256", PortraitSize, new Size(64, 256), "64,256" },
                { "0,0,512,256/!256,256", PortraitSize, new Size(256, 128), "!256,256" },
                { "pct:10,10,25,50/,256", PortraitSize, new Size(64, 256), ",256" },
                { "square/512,256", PortraitSize, new Size(512, 256), "512,256" },
                { "square/!256,512", PortraitSize, Size.Square(256), "!256,512" },
                { "square/pct:50", PortraitSize, Size.Square(500), "pct:50" },
                { "full/512,256", PortraitSize, new Size(512, 256), "512,256" },
            };
        
        /// <summary>
        /// V2 specific requests for portrait images. maxWidth is 5000.
        /// </summary>
        public static TheoryData<string, Size, Size, string> PortraitV2 =>
            new()
            {
                { "0,0,512,256/max", PortraitSize, new Size(5000, 2500), "5000,2500" },
                { "pct:10,10,25,50/max", PortraitSize, new Size(1250, 5000), "1250,5000" },
                { "0,0,512,256/full", PortraitSize, new Size(512, 256), "512,256" },
                { "square/pct:250", PortraitSize, Size.Square(2500), "pct:250" },
                { "square/max", PortraitSize, Size.Square(5000), "5000,5000" },
                { "square/full", PortraitSize, Size.Square(1000), "1000,1000" },
                { "full/full", PortraitSize, new Size(1000, 2000), "1000,2000" },
                { "full/max", PortraitSize, new Size(2500, 5000), "2500,5000" },
            };
        
        /// <summary>
        /// V3 specific requests for portrait images. maxWidth is 5000.
        /// </summary>
        public static TheoryData<string, Size, Size, string> PortraitV3 =>
            new()
            {
                { "0,0,512,256/max", PortraitSize, new Size(512, 256), "512,256" },
                { "0,0,512,256/^max", PortraitSize, new Size(5000, 2500), "^5000,2500" },
                { "pct:10,10,25,50/^max", PortraitSize, new Size(1250, 5000), "^1250,5000" },
                { "square/^pct:250", PortraitSize, Size.Square(2500), "^pct:250" },
                { "square/max", PortraitSize, Size.Square(1000), "1000,1000" },
                { "square/^max", PortraitSize, Size.Square(5000), "^5000,5000" },
                { "full/max", PortraitSize, new Size(1000, 2000), "1000,2000" },
                { "full/^max", PortraitSize, new Size(2500, 5000), "^2500,5000" },
            };
        
        /// <summary>
        /// A series of valid image requests and expected sizes for landscape images. maxWidth is 5000.
        ///
        /// Values are: imageRequest, imageSize, expectedSize, expectedProxySize
        /// </summary>
        public static TheoryData<string, Size, Size, string> LandscapeShared =>
            new()
            {
                { "100,100,512,512/512,", LandscapeSize, Size.Square(512), "512," },
                { "0,0,512,512/,256", LandscapeSize, Size.Square(256), ",256" },
                { "0,0,128,512/64,256", LandscapeSize, new Size(64, 256), "64,256" },
                { "0,0,512,256/!256,256", LandscapeSize, new Size(256, 128), "!256,256" },
                { "pct:10,10,25,50/,256", LandscapeSize, new Size(256, 256), ",256" },
                { "square/512,256", LandscapeSize, new Size(512, 256), "512,256" },
                { "square/!256,512", LandscapeSize, Size.Square(256), "!256,512" },
                { "square/pct:50", LandscapeSize, Size.Square(500), "pct:50" },
                { "full/512,256", LandscapeSize, new Size(512, 256), "512,256" },
            };
        
        /// <summary>
        /// V2 specific requests for landscape images. maxWidth is 5000.
        /// </summary>
        public static TheoryData<string, Size, Size, string> LandscapeV2 =>
            new()
            {
                { "0,0,512,256/full", LandscapeSize, new Size(512, 256), "512,256" },
                { "0,0,512,256/max", LandscapeSize, new Size(5000,2500), "5000,2500" },
                { "square/full", LandscapeSize, Size.Square(1000), "1000,1000" },
                { "square/max", LandscapeSize, Size.Square(5000), "5000,5000" },
                { "full/full", LandscapeSize, new Size(2000, 1000), "2000,1000" },
                { "full/max", LandscapeSize, new Size(5000,2500), "5000,2500" },
            };
        
        /// <summary>
        /// V3 specific requests for landscape images. maxWidth is 5000.
        /// </summary>
        public static TheoryData<string, Size, Size, string> LandscapeV3 =>
            new()
            {
                { "0,0,512,256/max", LandscapeSize, new Size(512, 256), "512,256" },
                { "0,0,512,256/^max", LandscapeSize, new Size(5000, 2500), "^5000,2500" },
                { "pct:10,10,25,50/^max", LandscapeSize, new Size(5000, 5000), "^5000,5000" },
                { "square/^pct:250", LandscapeSize, Size.Square(2500), "^pct:250" },
                { "square/max", LandscapeSize, Size.Square(1000), "1000,1000" },
                { "square/^max", LandscapeSize, Size.Square(5000), "^5000,5000" },
                { "full/max", LandscapeSize, new Size(2000, 1000), "2000,1000" },
                { "full/^max", LandscapeSize, new Size(5000, 2500), "^5000,2500" },
            };
        
        /// <summary>
        /// A series of valid image requests and expected sizes for Portrait images. maxWidth is 500.
        ///
        /// Values are: imageRequest, imageSize, expectedSize, expectedProxySize
        /// </summary>
        public static TheoryData<string, Size, Size, string> PortraitRestrictedMaxWidth =>
            new()
        {
            { "0,0,512,256/max", PortraitSize, new Size(500, 250), "500,250" },
            { "square/max", PortraitSize, Size.Square(500), "500,500" },
            { "full/max", PortraitSize, new Size(250, 500), "250,500" },
        };
        
        public static TheoryData<string, Size, Size, string> PortraitRestrictedMaxWidthV2 =>
            new()
            {
                { "0,0,512,256/full", PortraitSize, new Size(500, 250), "500,250" },
                { "square/full", PortraitSize, Size.Square(500), "500,500" },
                { "full/full", PortraitSize, new Size(250, 500), "250,500" },
            };
        
        public static TheoryData<string, Size, Size, string> PortraitRestrictedMaxWidthV3 =>
            new()
            {
                { "0,0,512,256/^max", PortraitSize, new Size(500, 250), "500,250" },
                { "pct:10,10,25,50/^max", PortraitSize, new Size(125, 500), "125,500" },
                { "square/^max", PortraitSize, Size.Square(500), "500,500" },
                { "full/^max", PortraitSize, new Size(250, 500), "250,500" },
            };
        
        /// <summary>
        /// A series of valid image requests and expected sizes for Landscape images. maxWidth is 500.
        ///
        /// Values are: imageRequest, imageSize, expectedSize, expectedProxySize
        /// </summary>
        public static TheoryData<string, Size, Size, string> LandscapeRestrictedMaxWidth =>
            new()
            {
                { "0,0,512,256/max", LandscapeSize, new Size(500, 250), "500,250" },
                { "square/max", LandscapeSize, Size.Square(500), "500,500" },
                { "full/max", LandscapeSize, new Size(500, 250), "500,250" },
            };
        
        public static TheoryData<string, Size, Size, string> LandscapeRestrictedMaxWidthV2 =>
            new()
            {
                { "0,0,512,256/full", LandscapeSize, new Size(500, 250), "500,250" },
                { "square/full", LandscapeSize, Size.Square(500), "500,500" },
                { "full/full", LandscapeSize, new Size(500, 250), "500,250" },
            };
        
        public static TheoryData<string, Size, Size, string> LandscapeRestrictedMaxWidthV3 =>
            new()
            {
                { "0,0,512,256/^max", LandscapeSize, new Size(500, 250), "500,250" },
                { "pct:10,10,25,50/^max", LandscapeSize, new Size(500, 500), "500,500" },
                { "square/^max", LandscapeSize, Size.Square(500), "500,500" },
                { "full/^max", LandscapeSize, new Size(500, 250), "500,250" },
            };
    }
}
