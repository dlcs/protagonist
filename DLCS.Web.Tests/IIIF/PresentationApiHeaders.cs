using System.Linq;
using DLCS.Web.IIIF;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;
using Presi = IIIF.Presentation;

namespace DLCS.Web.Tests.IIIF;

public class PresentationApiHeaders
{
    [Theory]
    [InlineData(Presi.Version.V2)]
    [InlineData(Presi.Version.V3)]
    [InlineData(Presi.Version.Unknown)]
    public void GetIIIFPresentationApiVersion_ReturnsFallback_IfHeadersNull(Presi.Version fallbackVersion)
    {
        var httpRequest = new DefaultHttpContext().Request;

        var version = httpRequest.GetIIIFPresentationApiVersion(fallbackVersion);

        version.Should().Be(fallbackVersion);
    }
    
    [Theory]
    [InlineData(Presi.Version.V2)]
    [InlineData(Presi.Version.V3)]
    [InlineData(Presi.Version.Unknown)]
    public void GetIIIFPresentationApiVersion_ReturnsFallback_IfHeadersEmpty(Presi.Version fallbackVersion)
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Accept = "";

        var version = httpRequest.GetIIIFPresentationApiVersion(fallbackVersion);

        version.Should().Be(fallbackVersion);
    }
    
    [Theory]
    [InlineData("application/ld+json;profile=\"http://iiif.io/api/presentation/3/context.json\"", Presi.Version.V3)]
    [InlineData("application/ld+json;profile=\"http://iiif.io/api/presentation/2/context.json\"", Presi.Version.V2)]
    public void GetIIIFPresentationApiVersion_ReturnsCorrect(string header, Presi.Version expected)
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Accept = header;
        
        var version = httpRequest.GetIIIFPresentationApiVersion(Presi.Version.Unknown);

        version.Should().Be(expected);
    }
    
    [Fact]
    public void GetIIIFPresentationApiVersion_PrefersHigherVersion_IfBothPresent()
    {
        var headers = new[]
        {
            "application/ld+json;profile=\"http://iiif.io/api/presentation/3/context.json\"",
            "application/ld+json;profile=\"http://iiif.io/api/presentation/2/context.json\""
        };
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Headers.Accept = new StringValues(headers);

        var version = httpRequest.GetIIIFPresentationApiVersion(Presi.Version.Unknown);
        version.Should().Be(Presi.Version.V3);
        
        // Now check opposite order of headers
        httpRequest.Headers.Accept = new StringValues(headers.Reverse().ToArray());

        var versionReverse = httpRequest.GetIIIFPresentationApiVersion(Presi.Version.Unknown);
        versionReverse.Should().Be(Presi.Version.V3);
    }
}