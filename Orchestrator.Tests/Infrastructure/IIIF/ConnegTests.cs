using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Net.Http.Headers;
using Orchestrator.Infrastructure.IIIF;
using Xunit;
using Presi = IIIF.Presentation;
using ImageApi = IIIF.ImageApi;

namespace Orchestrator.Tests.Infrastructure.IIIF;

public class ConnegTests
{
    [Theory]
    [InlineData(Presi.Version.V2)]
    [InlineData(Presi.Version.V3)]
    [InlineData(Presi.Version.Unknown)]
    public void GetIIIFPresentationType_ReturnsFallback_IfHeadersNull(Presi.Version fallbackVersion)
    {
        IEnumerable<MediaTypeHeaderValue> headers = null;

        var version = headers.GetIIIFPresentationType(fallbackVersion);

        version.Should().Be(fallbackVersion);
    }
    
    [Theory]
    [InlineData(Presi.Version.V2)]
    [InlineData(Presi.Version.V3)]
    [InlineData(Presi.Version.Unknown)]
    public void GetIIIFPresentationType_ReturnsFallback_IfHeadersEmpty(Presi.Version fallbackVersion)
    {
        var headers = Enumerable.Empty<MediaTypeHeaderValue>();

        var version = headers.GetIIIFPresentationType(fallbackVersion);

        version.Should().Be(fallbackVersion);
    }
    
    [Theory]
    [InlineData("application/ld+json;profile=\"http://iiif.io/api/presentation/3/context.json\"", Presi.Version.V3)]
    [InlineData("application/ld+json;profile=\"http://iiif.io/api/presentation/2/context.json\"", Presi.Version.V2)]
    public void GetIIIFPresentationType_ReturnsCorrect(string header, Presi.Version expected)
    {
        var headers = new List<MediaTypeHeaderValue>
        {
            MediaTypeHeaderValue.Parse(header)
        };

        var version = headers.GetIIIFPresentationType();

        version.Should().Be(expected);
    }
    
    [Fact]
    public void GetIIIFPresentationType_PrefersHigherVersion_IfBothPresent()
    {
        var headers = new List<MediaTypeHeaderValue>
        {
            MediaTypeHeaderValue.Parse("application/ld+json;profile=\"http://iiif.io/api/presentation/3/context.json\""),
            MediaTypeHeaderValue.Parse("application/ld+json;profile=\"http://iiif.io/api/presentation/2/context.json\"")
        };

        var version = headers.GetIIIFPresentationType();
        version.Should().Be(Presi.Version.V3);
        
        // Now check opposite order of headers
        headers.Reverse();

        var versionReverse = headers.GetIIIFPresentationType();
        versionReverse.Should().Be(Presi.Version.V3);
    }
    
    [Theory]
    [InlineData(ImageApi.Version.V2)]
    [InlineData(ImageApi.Version.V3)]
    [InlineData(ImageApi.Version.Unknown)]
    public void GetIIIFImageApiType_ReturnsFallback_IfHeadersNull(ImageApi.Version fallbackVersion)
    {
        IEnumerable<MediaTypeHeaderValue> headers = null;

        var version = headers.GetIIIFImageApiType(fallbackVersion);

        version.Should().Be(fallbackVersion);
    }
    
    [Theory]
    [InlineData(ImageApi.Version.V2)]
    [InlineData(ImageApi.Version.V3)]
    [InlineData(ImageApi.Version.Unknown)]
    public void GetIIIFImageApiType_ReturnsFallback_IfHeadersEmpty(ImageApi.Version fallbackVersion)
    {
        var headers = Enumerable.Empty<MediaTypeHeaderValue>();

        var version = headers.GetIIIFImageApiType(fallbackVersion);

        version.Should().Be(fallbackVersion);
    }
    
    [Theory]
    [InlineData("application/ld+json;profile=\"http://iiif.io/api/image/3/context.json\"", ImageApi.Version.V3)]
    [InlineData("application/ld+json;profile=\"http://iiif.io/api/image/2/context.json\"", ImageApi.Version.V2)]
    public void GetIIIFImageApiType_ReturnsCorrect(string header, ImageApi.Version expected)
    {
        var headers = new List<MediaTypeHeaderValue>
        {
            MediaTypeHeaderValue.Parse(header)
        };

        var version = headers.GetIIIFImageApiType();

        version.Should().Be(expected);
    }
    
    [Fact]
    public void GetIIIFImageApiType_PrefersHigherVersion_IfBothPresent()
    {
        var headers = new List<MediaTypeHeaderValue>
        {
            MediaTypeHeaderValue.Parse("application/ld+json;profile=\"http://iiif.io/api/image/3/context.json\""),
            MediaTypeHeaderValue.Parse("application/ld+json;profile=\"http://iiif.io/api/image/2/context.json\"")
        };

        var version = headers.GetIIIFImageApiType();
        version.Should().Be(ImageApi.Version.V3);
        
        // Now check opposite order of headers
        headers.Reverse();

        var versionReverse = headers.GetIIIFImageApiType();
        versionReverse.Should().Be(ImageApi.Version.V3);
    }
}