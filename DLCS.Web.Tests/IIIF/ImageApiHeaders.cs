using System.Collections.Generic;
using System.Linq;
using DLCS.Web.IIIF;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Xunit;
using ImageApi = IIIF.ImageApi;

namespace DLCS.Web.Tests.IIIF;

public class ImageApiHeaders
{
    [Theory]
    [InlineData(ImageApi.Version.V2)]
    [InlineData(ImageApi.Version.V3)]
    [InlineData(ImageApi.Version.Unknown)]
    public void GetIIIFImageApiVersion_ReturnsFallback_IfHeadersNull(ImageApi.Version fallbackVersion)
    {
        var httpRequest = new DefaultHttpContext().Request;

        var version = httpRequest.GetIIIFImageApiVersion(fallbackVersion);

        version.Should().Be(fallbackVersion);
    }
    
    [Theory]
    [InlineData(ImageApi.Version.V2)]
    [InlineData(ImageApi.Version.V3)]
    [InlineData(ImageApi.Version.Unknown)]
    public void GetIIIFImageApiVersion_ReturnsFallback_IfHeadersEmpty(ImageApi.Version fallbackVersion)
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Accept = "";

        var version = httpRequest.GetIIIFImageApiVersion(fallbackVersion);

        version.Should().Be(fallbackVersion);
    }
    
    [Theory]
    [InlineData("application/ld+json;profile=\"http://iiif.io/api/image/3/context.json\"", ImageApi.Version.V3)]
    [InlineData("application/ld+json;profile=\"http://iiif.io/api/image/2/context.json\"", ImageApi.Version.V2)]
    public void GetIIIFImageApiVersion_ReturnsCorrect(string header, ImageApi.Version expected)
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Accept = header;
        
        var version = httpRequest.GetIIIFImageApiVersion(ImageApi.Version.Unknown);

        version.Should().Be(expected);
    }
    
    [Fact]
    public void GetIIIFImageApiVersion_PrefersHigherVersion_IfBothPresent()
    {
        var headers = new[]
        {
            "application/ld+json;profile=\"http://iiif.io/api/image/3/context.json\"",
            "application/ld+json;profile=\"http://iiif.io/api/image/2/context.json\""
        };
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Accept = new StringValues(headers);

        var version = httpRequest.GetIIIFImageApiVersion(ImageApi.Version.Unknown);
        version.Should().Be(ImageApi.Version.V3);
        
        // Now check opposite order of headers
        httpRequest.Headers.Accept = new StringValues(headers.Reverse().ToArray());

        var versionReverse = httpRequest.GetIIIFImageApiVersion(ImageApi.Version.Unknown);
        versionReverse.Should().Be(ImageApi.Version.V3);
    }
}