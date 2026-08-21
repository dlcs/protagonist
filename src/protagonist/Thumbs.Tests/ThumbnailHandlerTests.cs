using FluentAssertions;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using FakeItEasy;
using IIIF.ImageApi;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetVips;
using Thumbs;
using Thumbs.Settings;

namespace Thumbs.Tests;

public class ThumbnailHandlerTests
{
    private static readonly AssetId AssetId = new(1, 2, "foo");

    private readonly IBucketReader bucketReader = A.Fake<IBucketReader>();
    private readonly IThumbRepository thumbRepository = A.Fake<IThumbRepository>();
    private readonly IStorageKeyGenerator storageKeyGenerator = A.Fake<IStorageKeyGenerator>();

    public ThumbnailHandlerTests()
    {
        // open sizes for the asset - 400x800 is the largest available to resize from
        A.CallTo(() => thumbRepository.GetOpenSizes(A<AssetId>._))
            .Returns([[400, 800], [200, 400], [100, 200]]);

        A.CallTo(() => storageKeyGenerator.GetThumbnailLocation(A<AssetId>._, A<int>._, A<bool>._))
            .Returns(new ObjectInBucket("thumbs-bucket", "1/2/foo/800.jpg"));
    }

    private ThumbnailHandler GetSut(bool upscale = false) =>
        new(new NullLogger<ThumbnailHandler>(), bucketReader,
            Options.Create(new ThumbsSettings { Resize = true, Upscale = upscale, UpscaleThreshold = 100 }),
            storageKeyGenerator, thumbRepository);

    /// <summary>
    /// A forward-only stream, as returned by S3 - no Length, no Seek, no Position. libvips consumes the source
    /// incrementally so this must work without the caller buffering it first
    /// </summary>
    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream inner = new(data);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
    }

    private static byte[] CreateJpeg(int width, int height)
    {
        using var image = Image.Black(width, height).Copy(interpretation: Enums.Interpretation.Bw);
        return image.JpegsaveBuffer();
    }

    private void ThumbnailInBucketIs(byte[] jpeg, bool seekable = false)
        => A.CallTo(() => bucketReader.GetObjectFromBucket(A<ObjectInBucket>._, A<CancellationToken>._))
            .ReturnsLazily((ObjectInBucket o, CancellationToken _) => new ObjectFromBucket(o,
                seekable ? new MemoryStream(jpeg) : new NonSeekableStream(jpeg), null));

    private static ImageRequest Confined(int width, int height) =>
        new() { Size = new SizeParameter { Width = width, Height = height, Confined = true } };

    [Fact]
    public async Task GetThumbnail_ResizesFromNonSeekableStream()
    {
        // Arrange - 150x300 is not an open size, so the 400x800 thumbnail is downscaled
        ThumbnailInBucketIs(CreateJpeg(400, 800));

        // Act
        await using var result = await GetSut().GetThumbnail(AssetId, Confined(150, 300));

        // Assert
        result.IsEmpty.Should().BeFalse("an S3 stream is forward-only and must not need buffering");
        result.WasResized.Should().BeTrue();

        using var resized = Image.NewFromStream(result.ThumbnailStream!);
        resized.Width.Should().Be(150);
        resized.Height.Should().Be(300);
    }

    [Fact]
    public async Task GetThumbnail_ProducesSameResult_ForSeekableAndNonSeekableSource()
    {
        // Arrange
        var jpeg = CreateJpeg(400, 800);

        // Act
        ThumbnailInBucketIs(jpeg, seekable: true);
        await using var fromSeekable = await GetSut().GetThumbnail(AssetId, Confined(150, 300));
        using var seekableOutput = new MemoryStream();
        await fromSeekable.ThumbnailStream!.CopyToAsync(seekableOutput);

        ThumbnailInBucketIs(jpeg);
        await using var fromNonSeekable = await GetSut().GetThumbnail(AssetId, Confined(150, 300));
        using var nonSeekableOutput = new MemoryStream();
        await fromNonSeekable.ThumbnailStream!.CopyToAsync(nonSeekableOutput);

        // Assert
        nonSeekableOutput.ToArray().Should().Equal(seekableOutput.ToArray());
    }

    [Fact]
    public async Task GetThumbnail_ReturnsExactSize_WithoutResizing_WhenSizeIsKnown()
    {
        // Arrange
        ThumbnailInBucketIs(CreateJpeg(200, 400));

        // Act - 200x400 is an open size
        await using var result = await GetSut().GetThumbnail(AssetId, Confined(200, 400));

        // Assert
        result.IsExactMatch.Should().BeTrue();
        result.WasResized.Should().BeFalse();
    }

    [Fact]
    public async Task GetThumbnail_ReturnsEmpty_WhenThumbnailHasNoContent()
    {
        // Arrange
        A.CallTo(() => bucketReader.GetObjectFromBucket(A<ObjectInBucket>._, A<CancellationToken>._))
            .ReturnsLazily((ObjectInBucket o, CancellationToken _) => new ObjectFromBucket(o, null, null));

        // Act
        await using var result = await GetSut().GetThumbnail(AssetId, Confined(150, 300));

        // Assert
        result.IsEmpty.Should().BeTrue("a missing source must not throw out of the handler");
    }
}
