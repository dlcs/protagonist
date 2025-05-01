using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using DLCS.Model.PathElements;
using DLCS.Web.Response;
using IIIF;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Infrastructure.IIIF.Manifests;
using Test.Helpers;
using Test.Helpers.Data;

namespace Orchestrator.Tests.Infrastructure.IIIF.Manifests;

public class ManifestV3BuilderTests
{
    private readonly IManifestBuilderUtils builderUtils;
    private readonly CustomerPathElement pathElement = new(99, "test");

    private readonly ManifestV3Builder sut;
    
    public ManifestV3BuilderTests()
    {
        builderUtils = A.Fake<IManifestBuilderUtils>();
        var assetPathGenerator = A.Fake<IAssetPathGenerator>();
        var authBuilder = A.Fake<IIIIFAuthBuilder>();

        A.CallTo(() => builderUtils.RetrieveThumbnails(A<Asset>._, A<CancellationToken>._))
            .Returns(new ImageSizeDetails(
                new List<Size> { new(150, 100), new(300, 200) },
                new(300, 200)));

        sut = new ManifestV3Builder(builderUtils, assetPathGenerator, authBuilder, new NullLogger<ManifestV3Builder>());
    }

    [Fact]
    public async Task BuildManifest_ImageOnly()
    {
        var asset = GetImageAsset("iiif-img");
        var manifestId = $"https://dlcs.test/iiif-manifest/{asset}";
        A.CallTo(() => builderUtils.GetFullQualifiedImagePath(asset, pathElement, A<Size>._, false))
            .Returns("https://dlcs.test/image-url/");
        var imageServices = new List<IService> { new ImageService3 { Id = "test-service" } };
        A.CallTo(() => builderUtils.GetImageServices(asset, pathElement, false, null))
            .Returns(imageServices);

        var manifest = await sut.BuildManifest(manifestId, "testLabel", asset.AsList(), pathElement,
            ManifestType.NamedQuery, CancellationToken.None);

        manifest.Id.Should().Be(manifestId);
        manifest.Thumbnail.Should().BeNull("No thumbnail delivery channel");
        var canvas = manifest.Items.Single();
        canvas.Thumbnail.Should().BeNull("No thumbnail delivery channel");
        canvas.Rendering.Should().BeNull("No file delivery channel");
        
        var image = canvas.GetCanvasPaintingBody<Image>();
        image.Width.Should().Be(300, "Width of largest derivative");
        image.Height.Should().Be(200, "Height of largest derivative");
        image.Id.Should().Be("https://dlcs.test/image-url/");
        image.Service.Should().BeEquivalentTo(imageServices, "Verify it sets results of GetImageServices()");
    }
    
    [Fact]
    public async Task BuildManifest_ImageThumb()
    {
        var asset = GetImageAsset("iiif-img,thumbs");
        
        var manifestId = $"https://dlcs.test/iiif-manifest/{asset}";
        A.CallTo(() => builderUtils.GetFullQualifiedImagePath(asset, pathElement, A<Size>._, false))
            .Returns("https://dlcs.test/image-url/");
        var imageServices = new List<IService> { new ImageService3 { Id = "test-service" } };
        A.CallTo(() => builderUtils.GetImageServices(asset, pathElement, false, null))
            .Returns(imageServices);
        A.CallTo(() => builderUtils.ShouldAddThumbs(asset, A<ImageSizeDetails>._)).Returns(true);

        var manifest = await sut.BuildManifest(manifestId, "testLabel", asset.AsList(), pathElement,
            ManifestType.NamedQuery, CancellationToken.None);

        manifest.Id.Should().Be(manifestId);
        manifest.Thumbnail.Should().NotBeNull("Has thumbnail delivery channel");
        
        var canvas = manifest.Items.Single();
        canvas.Thumbnail.Should().NotBeNull("Has thumbnail delivery channel");
        canvas.Rendering.Should().BeNull("No file delivery channel");
        
        var image = canvas.GetCanvasPaintingBody<Image>();
        image.Width.Should().Be(300, "Width of largest derivative");
        image.Height.Should().Be(200, "Height of largest derivative");
        image.Id.Should().Be("https://dlcs.test/image-url/");
        image.Service.Should().BeEquivalentTo(imageServices, "Verify it sets results of GetImageServices()");
    }
    
    [Fact]
    public async Task BuildManifest_ImageThumbFile()
    {
        var asset = GetImageAsset("iiif-img,thumbs,file");
        
        var manifestId = $"https://dlcs.test/iiif-manifest/{asset}";
        A.CallTo(() => builderUtils.GetFullQualifiedImagePath(asset, pathElement, A<Size>._, false))
            .Returns("https://dlcs.test/image-url/");
        var imageServices = new List<IService> { new ImageService3 { Id = "test-service" } };
        A.CallTo(() => builderUtils.GetImageServices(asset, pathElement, false, null))
            .Returns(imageServices);
        A.CallTo(() => builderUtils.ShouldAddThumbs(asset, A<ImageSizeDetails>._)).Returns(true);

        var manifest = await sut.BuildManifest(manifestId, "testLabel", asset.AsList(), pathElement,
            ManifestType.NamedQuery, CancellationToken.None);

        manifest.Id.Should().Be(manifestId);
        manifest.Thumbnail.Should().NotBeNull("Has thumbnail delivery channel");
        
        var canvas = manifest.Items.Single();
        canvas.Thumbnail.Should().NotBeNull("Has thumbnail delivery channel");
        var rendering = canvas.Rendering.Single().As<Image>();
        rendering.Should().NotBeNull("Has file delivery channel");
        rendering.Width.Should().Be(1500, "Width of origin");
        rendering.Height.Should().Be(1000, "Height of origin");
        
        var image = canvas.GetCanvasPaintingBody<Image>();
        image.Width.Should().Be(300, "Width of largest derivative");
        image.Height.Should().Be(200, "Height of largest derivative");
        image.Id.Should().Be("https://dlcs.test/image-url/");
        image.Service.Should().BeEquivalentTo(imageServices, "Verify it sets results of GetImageServices()");
    }
    
    [Fact]
    public async Task BuildManifest_Thumb()
    {
        var asset = GetImageAsset("thumbs");
        
        var manifestId = $"https://dlcs.test/iiif-manifest/{asset}";
        A.CallTo(() => builderUtils.GetFullQualifiedImagePath(asset, pathElement, A<Size>._, true))
            .Returns("https://dlcs.test/thumbs-url/");
        A.CallTo(() => builderUtils.ShouldAddThumbs(asset, A<ImageSizeDetails>._)).Returns(true);

        var manifest = await sut.BuildManifest(manifestId, "testLabel", asset.AsList(), pathElement,
            ManifestType.NamedQuery, CancellationToken.None);

        manifest.Id.Should().Be(manifestId);
        manifest.Thumbnail.Should().NotBeNull("Has thumbnail delivery channel");
        
        var canvas = manifest.Items.Single();
        canvas.Thumbnail.Should().NotBeNull("Has thumbnail delivery channel");
        canvas.Rendering.Should().BeNull("No file delivery channel");
        
        var image = canvas.GetCanvasPaintingBody<Image>();
        image.Width.Should().Be(300, "Width of largest derivative");
        image.Height.Should().Be(200, "Height of largest derivative");
        image.Id.Should().Be("https://dlcs.test/thumbs-url/");
        image.Service.Should().BeNull("No image delivery channel");
    }
    
    [Fact]
    public async Task BuildManifest_ThumbFile()
    {
        var asset = GetImageAsset("thumbs,file");
        
        var manifestId = $"https://dlcs.test/iiif-manifest/{asset}";
        A.CallTo(() => builderUtils.GetFullQualifiedImagePath(asset, pathElement, A<Size>._, true))
            .Returns("https://dlcs.test/thumbs-url/");
        A.CallTo(() => builderUtils.ShouldAddThumbs(asset, A<ImageSizeDetails>._)).Returns(true);

        var manifest = await sut.BuildManifest(manifestId, "testLabel", asset.AsList(), pathElement,
            ManifestType.NamedQuery, CancellationToken.None);

        manifest.Id.Should().Be(manifestId);
        manifest.Thumbnail.Should().NotBeNull("Has thumbnail delivery channel");
        
        var canvas = manifest.Items.Single();
        canvas.Thumbnail.Should().NotBeNull("Has thumbnail delivery channel");
        var rendering = canvas.Rendering.Single().As<Image>();
        rendering.Should().NotBeNull("Has file delivery channel");
        rendering.Width.Should().Be(1500, "Width of origin");
        rendering.Height.Should().Be(1000, "Height of origin");
        
        var image = canvas.GetCanvasPaintingBody<Image>();
        image.Width.Should().Be(300, "Width of largest derivative");
        image.Height.Should().Be(200, "Height of largest derivative");
        image.Id.Should().Be("https://dlcs.test/thumbs-url/");
        image.Service.Should().BeNull("No image delivery channel");
    }
    
    [Fact]
    public async Task BuildManifest_TimebasedOnly_Audio()
    {
        var asset = new Asset
        {
            Id = AssetIdGenerator.GetAssetId(),
            MediaType = "audio/wav",
            Duration = 15000,
            ImageDeliveryChannels = "iiif-av".GenerateDeliveryChannels()
        }.WithTestTranscodeMetadata(new AVTranscode
        {
            Duration = 14666, MediaType = "audio/mp3",
            Location = new Uri("s3://dlcs-storage/2/1/foo/full/max/default.mp3")
        }.AsList());
        
        var manifestId = $"https://dlcs.test/iiif-manifest/{asset}";

        var manifest = await sut.BuildManifest(manifestId, "testLabel", asset.AsList(), pathElement,
            ManifestType.NamedQuery, CancellationToken.None);

        manifest.Id.Should().Be(manifestId);
        manifest.Thumbnail.Should().BeNull("No thumbnail delivery channel");
        
        var canvas = manifest.Items.Single();
        canvas.Thumbnail.Should().BeNull("No thumbnail delivery channel");
        canvas.Rendering.Should().BeNull("No file delivery channel");
        
        var sound = canvas.GetCanvasPaintingBody<Sound>();
        sound.Duration.Should().Be(14666, "Duration of transcode");
    }
    
    [Fact]
    public async Task BuildManifest_TimebasedFile_Audio()
    {
        var asset = new Asset
        {
            Id = AssetIdGenerator.GetAssetId(),
            MediaType = "audio/wav",
            Duration = 15000,
            ImageDeliveryChannels = "iiif-av,file".GenerateDeliveryChannels()
        }.WithTestTranscodeMetadata(new AVTranscode
        {
            Duration = 14666, MediaType = "audio/mp3",
            Location = new Uri("s3://dlcs-storage/2/1/foo/full/max/default.mp3")
        }.AsList());
        
        var manifestId = $"https://dlcs.test/iiif-manifest/{asset}";

        var manifest = await sut.BuildManifest(manifestId, "testLabel", asset.AsList(), pathElement,
            ManifestType.NamedQuery, CancellationToken.None);

        manifest.Id.Should().Be(manifestId);
        manifest.Thumbnail.Should().BeNull("No thumbnail delivery channel");
        
        var canvas = manifest.Items.Single();
        canvas.Thumbnail.Should().BeNull("No thumbnail delivery channel");
        var rendering = canvas.Rendering.Single().As<Sound>();
        rendering.Should().NotBeNull("Has file delivery channel");
        rendering.Duration.Should().Be(15000, "Duration of origin");
        rendering.Format.Should().Be("audio/wav", "Mediatype of origin");
        
        var sound = canvas.GetCanvasPaintingBody<Sound>();
        sound.Duration.Should().Be(14666, "Duration of transcode");
    }
    
    [Fact]
    public async Task BuildManifest_TimebasedOnly_VideoMultiple()
    {
        var asset = new Asset
        {
            Id = AssetIdGenerator.GetAssetId(),
            MediaType = "video/raw",
            Duration = 15000,
            Width = 800,
            Height = 800,
            ImageDeliveryChannels = "iiif-av".GenerateDeliveryChannels()
        }.WithTestTranscodeMetadata(new List<AVTranscode>
        {
            new()
            {
                Duration = 14666, MediaType = "video/mp4", Width = 400, Height = 401,
                Location = new Uri("s3://dlcs-storage/2/1/foo/full/full/max/max/0/default.mp4")
            },
            new()
            {
                Duration = 15002, MediaType = "video/mpeg", Width = 500, Height = 500,
                Location = new Uri("s3://dlcs-storage/2/1/foo/full/full/max/max/0/default.mpeg")
            }
        });
        
        var manifestId = $"https://dlcs.test/iiif-manifest/{asset}";

        var manifest = await sut.BuildManifest(manifestId, "testLabel", asset.AsList(), pathElement,
            ManifestType.NamedQuery, CancellationToken.None);

        manifest.Id.Should().Be(manifestId);
        manifest.Thumbnail.Should().BeNull("No thumbnail delivery channel");
        
        var canvas = manifest.Items.Single();
        canvas.Thumbnail.Should().BeNull("No thumbnail delivery channel");
        canvas.Rendering.Should().BeNull("No file delivery channel");
        
        var choice = canvas.GetCanvasPaintingBody<PaintingChoice>();
        choice.Items.Should().HaveCount(2, "2 transcodes for video");
        var first = choice.Items[0].As<Video>();
        first.Duration.Should().Be(14666, "From first transcode");
        first.Format.Should().Be("video/mp4", "From first transcode");
        first.Width.Should().Be(400, "From first transcode");
        first.Height.Should().Be(401, "From first transcode");
        var second = choice.Items[1].As<Video>();
        second.Duration.Should().Be(15002, "From second transcode");
        second.Format.Should().Be("video/mpeg", "From second transcode");
        second.Width.Should().Be(500, "From second transcode");
        second.Height.Should().Be(500, "From second transcode");
    }
    
    [Fact]
    public async Task BuildManifest_TimebasedFile_VideoMultiple()
    {
        var asset = new Asset
        {
            Id = AssetIdGenerator.GetAssetId(),
            MediaType = "video/raw",
            Duration = 15000,
            Width = 800,
            Height = 800,
            ImageDeliveryChannels = "iiif-av,file".GenerateDeliveryChannels()
        }.WithTestTranscodeMetadata(new List<AVTranscode>
        {
            new()
            {
                Duration = 14666, MediaType = "video/mp4", Width = 400, Height = 401,
                Location = new Uri("s3://dlcs-storage/2/1/foo/full/full/max/max/0/default.mp4")
            },
            new()
            {
                Duration = 15002, MediaType = "video/mpeg", Width = 500, Height = 500,
                Location = new Uri("s3://dlcs-storage/2/1/foo/full/full/max/max/0/default.mpeg")
            }
        });
        
        var manifestId = $"https://dlcs.test/iiif-manifest/{asset}";

        var manifest = await sut.BuildManifest(manifestId, "testLabel", asset.AsList(), pathElement,
            ManifestType.NamedQuery, CancellationToken.None);

        manifest.Id.Should().Be(manifestId);
        manifest.Thumbnail.Should().BeNull("No thumbnail delivery channel");
        
        var canvas = manifest.Items.Single();
        canvas.Thumbnail.Should().BeNull("No thumbnail delivery channel");
        var rendering = canvas.Rendering.Single().As<Video>();
        rendering.Should().NotBeNull("Has file delivery channel");
        rendering.Duration.Should().Be(15000, "Duration of origin");
        rendering.Width.Should().Be(800, "Width of origin");
        rendering.Height.Should().Be(800, "Height of origin");
        rendering.Format.Should().Be("video/raw", "Mediatype of origin");
        
        var choice = canvas.GetCanvasPaintingBody<PaintingChoice>();
        choice.Items.Should().HaveCount(2, "2 transcodes for video");
        var first = choice.Items[0].As<Video>();
        first.Duration.Should().Be(14666, "From first transcode");
        first.Format.Should().Be("video/mp4", "From first transcode");
        first.Width.Should().Be(400, "From first transcode");
        first.Height.Should().Be(401, "From first transcode");
        var second = choice.Items[1].As<Video>();
        second.Duration.Should().Be(15002, "From second transcode");
        second.Format.Should().Be("video/mpeg", "From second transcode");
        second.Width.Should().Be(500, "From second transcode");
        second.Height.Should().Be(500, "From second transcode");
    }
    
    [Fact]
    public async Task BuildManifest_TimebasedOnly_NoAssetApplicationMetadata()
    {
        var asset = new Asset
        {
            Id = AssetIdGenerator.GetAssetId(),
            MediaType = "video/raw",
            Duration = 15000,
            Width = 800,
            Height = 800,
            ImageDeliveryChannels = "iiif-av".GenerateDeliveryChannels()
        };
        
        var manifestId = $"https://dlcs.test/iiif-manifest/{asset}";

        var manifest = await sut.BuildManifest(manifestId, "testLabel", asset.AsList(), pathElement,
            ManifestType.NamedQuery, CancellationToken.None);

        manifest.Id.Should().Be(manifestId);
        manifest.Thumbnail.Should().BeNull("No thumbnail delivery channel");
        manifest.Items.Should().BeNullOrEmpty("No asset application metadata");
    }
    
    [Fact]
    public async Task BuildManifest_File()
    {
        var asset = GetImageAsset("file");
        
        var manifestId = $"https://dlcs.test/iiif-manifest/{asset}";
        A.CallTo(() => builderUtils.GetFullQualifiedImagePath(asset, pathElement, A<Size>._, false))
            .Returns("https://dlcs.test/image-url/");
        A.CallTo(() => builderUtils.GetCanvasId(asset, pathElement, A<int>._))
            .Returns("https://dlcs.test/canvas/0");

        var manifest = await sut.BuildManifest(manifestId, "testLabel", asset.AsList(), pathElement,
            ManifestType.NamedQuery, CancellationToken.None);

        manifest.Id.Should().Be(manifestId);
        manifest.Thumbnail.Should().BeNull("No thumbnail delivery channel");
        manifest.Context.As<List<string>>().Should()
            .Contain("https://iiif.wellcomecollection.org/extensions/born-digital/context.json",
                "Using custom behaviours");
        
        var canvas = manifest.Items.Single();
        canvas.Thumbnail.Should().BeNull("No thumbnail delivery channel");
        canvas.Behavior.Should().Contain("placeholder", "Placeholder canvas for original");
        var rendering = canvas.Rendering.Single().As<Image>();
        rendering.Should().NotBeNull("Has file delivery channel");
        rendering.Behavior.Should().Contain("original");
        rendering.Width.Should().Be(1500, "Width of origin");
        rendering.Height.Should().Be(1000, "Height of origin");
        rendering.Format.Should().Be("image/tiff", "Format of origin");
        
        var image = canvas.GetCanvasPaintingBody<Image>();
        image.Width.Should().Be(1000, "Width of static placeholder");
        image.Height.Should().Be(1000, "Height of static placeholder");
        image.Format.Should().Be("image/png", "Type of static placeholder");
        image.Service.Should().BeNull("No image service");
    }
    
    private static Asset GetImageAsset(string deliveryChannels) =>
        new()
        {
            Id = AssetIdGenerator.GetAssetId(),
            MediaType = "image/tiff",
            Width = 1500,
            Height = 1000,
            ImageDeliveryChannels = deliveryChannels.GenerateDeliveryChannels()
        };
}
