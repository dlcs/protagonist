using System.Collections.Generic;
using DLCS.Core.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Orchestrator.Features.Images;

namespace Orchestrator.Tests.Features.Images;

public class DownloadFilenameTests
{
    private static readonly AssetId AssetId = new(2, 1, "foo");

    [Fact]
    public void TryGetContentDisposition_False_IfNoQueryParam()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["other"] = "{id}" });

        DownloadFilename.TryGetContentDisposition(query, AssetId, "jpg", out var headerValue).Should().BeFalse();
        headerValue.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("id")]
    [InlineData("myfile.jpg")]
    [InlineData("{reference1}")]
    [InlineData("{id}-foo")]
    [InlineData("foo-{id}")]
    [InlineData(" {id}")]
    [InlineData("{{id}}")]
    public void TryGetContentDisposition_False_IfUnsupportedValue(string value)
    {
        var query = GetQuery(value);

        DownloadFilename.TryGetContentDisposition(query, AssetId, "jpg", out var headerValue).Should().BeFalse();
        headerValue.Should().BeNull();
    }

    [Fact]
    public void TryGetContentDisposition_False_IfMultipleValues()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["filename"] = new(["{id}", "{id}"])
        });

        DownloadFilename.TryGetContentDisposition(query, AssetId, "jpg", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("{id}")]
    [InlineData("{ID}")]
    [InlineData("{Id}")]
    public void TryGetContentDisposition_True_IfIdPlaceholder(string value)
    {
        var query = GetQuery(value);

        DownloadFilename.TryGetContentDisposition(query, AssetId, "jpg", out var headerValue).Should().BeTrue();

        var contentDisposition = ContentDispositionHeaderValue.Parse(headerValue);
        contentDisposition.DispositionType.Value.Should().Be("attachment");
        contentDisposition.FileName.Value.Should().Be("2_1_foo.jpg");
        contentDisposition.FileNameStar.Value.Should().Be("2_1_foo.jpg");
    }

    [Theory]
    [InlineData("foo", "png", "2_1_foo.png")]
    [InlineData("foo.png", "png", "2_1_foo.png.png")]
    [InlineData("image.jpeg", "png", "2_1_image.jpeg.png")]
    [InlineData("foo.tiff", "jpg", "2_1_foo.tiff.jpg")]
    [InlineData("a/b", "jpg", "2_1_a_b.jpg")]
    public void TryGetContentDisposition_UsesRequestFormatAsExtension(string asset, string format, string expected)
    {
        var query = GetQuery("{id}");

        DownloadFilename.TryGetContentDisposition(query, new AssetId(2, 1, asset), format, out var headerValue)
            .Should().BeTrue();

        ContentDispositionHeaderValue.Parse(headerValue).FileNameStar.Value.Should().Be(expected);
    }

    [Fact]
    public void TryGetContentDisposition_SetsEncodedFilenameStar_IfNonAscii()
    {
        var query = GetQuery("{id}");

        DownloadFilename.TryGetContentDisposition(query, new AssetId(2, 1, "café"), "jpg", out var headerValue)
            .Should().BeTrue();

        headerValue.Should().Contain("filename*=UTF-8''2_1_caf%C3%A9.jpg");
        ContentDispositionHeaderValue.Parse(headerValue).FileNameStar.Value.Should().Be("2_1_café.jpg");
    }

    private static QueryCollection GetQuery(string value)
        => new(new Dictionary<string, StringValues> { [DownloadFilename.QueryParameter] = value });
}
