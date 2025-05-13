using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3.Content;
using IIIF.Serialisation;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Tests.Infrastructure.IIIF.Manifests;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers;
using Test.Helpers.Data;
using Test.Helpers.Integration;
using IIIF3 = IIIF.Presentation.V3;

namespace Orchestrator.Tests.Integration;

/// <summary>
/// Test of iiif-manifest handling when rewriting is enabled. Only tests rewrite logic, see
/// <see cref="ManifestHandlingTests"/> and <see cref="NamedQueryTests"/> for full tests
/// </summary>
/// <remarks>
/// These tests verify single asset manifest only, using common DC configurations.
/// <see cref="ManifestV3BuilderTests"/> for more indepth tests of all DC permutations.</remarks>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class ManifestRewriteEnabledTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;
    private readonly List<ImageDeliveryChannel> imageDeliveryChannels;
    
    public ManifestRewriteEnabledTests(ProtagonistAppFactory<Startup> factory, DlcsDatabaseFixture databaseFixture)
    {
        dbFixture = databaseFixture;

        httpClient = factory
            .WithTestServices(services =>
            {
                services.AddSingleton<IIIIFAuthBuilder, FakeAuth2Client>();
            })
            .WithConnectionString(dbFixture.ConnectionString)
            .WithConfigValue("RewriteAssetPathsOnManifests", true.ToString())
            .CreateClient();
        
        imageDeliveryChannels = dbFixture.DbContext.GetImageDeliveryChannels();
        
        dbFixture.CleanUp();
    }

    [Theory]
    [InlineData("my-proxy.com", "1", "/const_value/99/|asset|", "/const_value/v2/99/|asset|")]
    [InlineData("versioned.com", "2", "/th/_|asset|", "/th/v2_|asset|")]
    [InlineData("non-versioned.com", "3", "/thumbs/99/|asset|", "/thumbs/99/|asset|")]
    public async Task Get_V2ManifestForImage_ReturnsManifest_PathsRewritten_ForThumb(string hostname, string postfix,
        string expectedThumb, string expectedService)
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId(assetPostfix: postfix);
        expectedThumb = $"http://{hostname}{expectedThumb.Replace("|asset|", id.Asset)}";
        expectedService = $"http://{hostname}{expectedService.Replace("|asset|", id.Asset)}";
        await dbFixture.DbContext.Images.AddTestAsset(id, imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();

        var path = $"iiif-manifest/v2/{id}";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Host", hostname);
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should()
            .Be($"http://{hostname}/iiif-manifest/v2/{id}", "Manifest id never changed");
        jsonResponse.SelectToken("sequences[0].canvases[0].@id").ToString().Should()
            .Be($"http://{hostname}/iiif-img/{id}/canvas/c/1", "Canvas id never changed");
        
        ValidateThumbnail(jsonResponse.SelectToken("sequences[0].canvases[0].thumbnail"), "Canvas thumbnail rewritten");
        ValidateThumbnail(jsonResponse.SelectToken("thumbnail"), "Manifest thumbnail rewritten");

        void ValidateThumbnail(JToken jToken, string because)
        {
            // thumbnail.@id will be path to a jpeg
            jToken.SelectToken("@id").Value<string>().Should()
                .Be($"{expectedThumb}/full/200,200/0/default.jpg", because);

            // service.@id will be path to a image service
            jToken.SelectToken("service.@id").Value<string>().Should().Be(expectedService, because);
        }
    }

    [Theory]
    [InlineData("my-proxy.com", "1", "/const_value/99/|asset|", "/const_value/v2/99/|asset|")]
    [InlineData("versioned.com", "2", "/image/_|asset|", "/image/v2_|asset|")]
    [InlineData("non-versioned.com", "3", "/image/99/|asset|", "/image/99/|asset|")]
    public async Task Get_V2ManifestForImage_ReturnsManifest_PathsRewritten_ForImage(string hostname, string postfix,
        string expectedThumb, string expectedService)
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId(assetPostfix: postfix);
        expectedThumb = $"http://{hostname}{expectedThumb.Replace("|asset|", id.Asset)}";
        expectedService = $"http://{hostname}{expectedService.Replace("|asset|", id.Asset)}";
        await dbFixture.DbContext.Images.AddTestAsset(id, imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();

        var path = $"iiif-manifest/v2/{id}";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Host", hostname);
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should()
            .Be($"http://{hostname}/iiif-manifest/v2/{id}", "Manifest id never changed");
        jsonResponse.SelectToken("sequences[0].canvases[0].@id").ToString().Should()
            .Be($"http://{hostname}/iiif-img/{id}/canvas/c/1", "Canvas id never changed");
        
        var imageResource = jsonResponse.SelectToken("sequences[0].canvases[0].images[0].resource");
        imageResource.SelectToken("@id").Value<string>().Should()
            .Be($"{expectedThumb}/full/1024,1024/0/default.jpg", "@id is jpeg");
        imageResource.SelectToken("service.@id").Value<string>().Should()
            .Be(expectedService, "service@id is image service");
    }
    
    [Theory]
    [InlineData("my-proxy.com", "1", "/const_value/99/|asset|", "/const_value/v2/99/|asset|")]
    [InlineData("versioned.com", "2", "/th/_|asset|", "/th/v2_|asset|")]
    public async Task Get_V3ManifestForImage_ReturnsManifest_PathsRewritten_ForThumb_IfTemplateVersioned(string hostname, string postfix,
        string expectedThumb, string expectedService)
    {
        // See https://github.com/dlcs/protagonist/issues/983 - if pathTemplate has {version} then all ImageService
        // paths are rewritten to match that format
        
        // Arrange
        var id = AssetIdGenerator.GetAssetId(assetPostfix: postfix);
        expectedThumb = $"http://{hostname}{expectedThumb.Replace("|asset|", id.Asset)}";
        var (expectedService2, expectedService3) = GetImageVersionSpecific(hostname, expectedService, id);
        await dbFixture.DbContext.Images.AddTestAsset(id, imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();

        var path = $"iiif-manifest/v3/{id}";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Host", hostname);
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var manifest = (await response.Content.ReadAsStreamAsync()).FromJsonStream<IIIF3.Manifest>();

        manifest.Id.Should().Be($"http://{hostname}/iiif-manifest/v3/{id}", "Manifest id never changed");
        
        var canvas = manifest.Items.Single();
        canvas.Id.Should().Be($"http://{hostname}/iiif-img/{id}/canvas/c/1", "Canvas id never changed");
        ValidateThumbnail(canvas.Thumbnail, "Canvas thumbnail rewritten");
        ValidateThumbnail(manifest.Thumbnail, "Manifest thumbnail rewritten");

        void ValidateThumbnail(List<ExternalResource> thumbnails, string because)
        {
            var thumbnail = thumbnails.Single();
            
            // thumbnail.id will be path to a jpeg
            thumbnail.Id.Should().Be($"{expectedThumb}/full/200,200/0/default.jpg", because);

            // imageService2 id will be path to a image service. This is not canonical so contains version
            var image2 = thumbnail.GetService<ImageService2>();
            image2.Id.Should().Be(expectedService2, because);
            
            // imageService3 id will be path to a image service. This is canonical so doesn't contain version
            var image3 = thumbnail.GetService<ImageService3>();
            image3.Id.Should().Be(expectedService3, because);
        }
    }
    
    [Theory]
    [InlineData("my-proxy.com", "1", "/const_value/99/|asset|", "/const_value/v2/99/|asset|")]
    [InlineData("versioned.com", "2", "/image/_|asset|", "/image/v2_|asset|")]
    public async Task Get_V3ManifestForImage_ReturnsManifest_PathsRewritten_ForImage_IfTemplateVersioned(string hostname, string postfix,
        string expectedImg, string expectedService)
    {
        // See https://github.com/dlcs/protagonist/issues/983 - if pathTemplate has {version} then all ImageService
        // paths are rewritten to match that format
        
        // Arrange
        var id = AssetIdGenerator.GetAssetId(assetPostfix: postfix);
        expectedImg = $"http://{hostname}{expectedImg.Replace("|asset|", id.Asset)}";
        var (expectedService2, expectedService3) = GetImageVersionSpecific(hostname, expectedService, id);
        await dbFixture.DbContext.Images.AddTestAsset(id, imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();

        var path = $"iiif-manifest/v3/{id}";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Host", hostname);
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var manifest = (await response.Content.ReadAsStreamAsync()).FromJsonStream<IIIF3.Manifest>();
        manifest.Id.Should().Be($"http://{hostname}/iiif-manifest/v3/{id}", "Manifest id never changed");
        
        var canvas = manifest.Items.Single();
        canvas.Id.Should().Be($"http://{hostname}/iiif-img/{id}/canvas/c/1", "Canvas id never changed");

        var imageResource = canvas.GetCanvasPaintingBody<Image>();
        imageResource.Id.Should().Be($"{expectedImg}/full/1024,1024/0/default.jpg", "id is jpeg");
        
        var image2 = imageResource.GetService<ImageService2>();
        image2.Id.Should().Be(expectedService2, "ImageService2 is image service with version");
            
        // imageService3 id will be path to a image service. This is canonical so doesn't contain version
        var image3 = imageResource.GetService<ImageService3>();
        image3.Id.Should().Be(expectedService3, "ImageService3 is image service with version");
    }
    
    [Theory]
    [InlineData("non-versioned.com", "1", "/thumbs/99/|asset|", "/thumbs/99/|asset|")]
    public async Task Get_V3ManifestForImage_ReturnsManifest_PathsRewritten_ForThumb_NotVersioned(string hostname, string postfix,
        string expectedThumb, string expectedService)
    {
        // See https://github.com/dlcs/protagonist/issues/983 - if pathTemplate doesn't have {version} then all
        // only the default/canonical ImageService (in this case v3) is rewritten to match that format, the rest use
        // canonical
        
        // Arrange
        var id = AssetIdGenerator.GetAssetId(assetPostfix: postfix);
        expectedThumb = $"http://{hostname}{expectedThumb.Replace("|asset|", id.Asset)}";
        var (_, expectedService3) = GetImageVersionSpecific(hostname, expectedService, id);
        var expectedService2 = $"http://{hostname}/thumbs/v2/{id}";
        await dbFixture.DbContext.Images.AddTestAsset(id, imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();

        var path = $"iiif-manifest/v3/{id}";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Host", hostname);
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var manifest = (await response.Content.ReadAsStreamAsync()).FromJsonStream<IIIF3.Manifest>();

        manifest.Id.Should().Be($"http://{hostname}/iiif-manifest/v3/{id}", "Manifest id never changed");
        
        var canvas = manifest.Items.Single();
        canvas.Id.Should().Be($"http://{hostname}/iiif-img/{id}/canvas/c/1", "Canvas id never changed");
        ValidateThumbnail(canvas.Thumbnail, "Canvas thumbnail rewritten");
        ValidateThumbnail(manifest.Thumbnail, "Manifest thumbnail rewritten");

        void ValidateThumbnail(List<ExternalResource> thumbnails, string because)
        {
            var thumbnail = thumbnails.Single();
            
            // thumbnail.id will be path to a jpeg
            thumbnail.Id.Should().Be($"{expectedThumb}/full/200,200/0/default.jpg", because);

            // imageService2 id will be path to a image service. This is not canonical so contains version
            var image2 = thumbnail.GetService<ImageService2>();
            image2.Id.Should().Be(expectedService2, because);
            
            // imageService3 id will be path to a image service. This is canonical so doesn't contain version
            var image3 = thumbnail.GetService<ImageService3>();
            image3.Id.Should().Be(expectedService3, because);
        }
    }
    
    [Theory]
    [InlineData("non-versioned.com", "3", "/image/99/|asset|", "/image/99/|asset|")]
    public async Task Get_V3ManifestForImage_ReturnsManifest_PathsRewritten_ForImage_NotVersioned(string hostname, string postfix,
        string expectedImg, string expectedService)
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId(assetPostfix: postfix);
        expectedImg = $"http://{hostname}{expectedImg.Replace("|asset|", id.Asset)}";
        var (_, expectedService3) = GetImageVersionSpecific(hostname, expectedService, id);
        var expectedService2 = $"http://{hostname}/iiif-img/v2/{id}";
        await dbFixture.DbContext.Images.AddTestAsset(id, imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();

        var path = $"iiif-manifest/v3/{id}";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Host", hostname);
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var manifest = (await response.Content.ReadAsStreamAsync()).FromJsonStream<IIIF3.Manifest>();
        manifest.Id.Should().Be($"http://{hostname}/iiif-manifest/v3/{id}", "Manifest id never changed");
        
        var canvas = manifest.Items.Single();
        canvas.Id.Should().Be($"http://{hostname}/iiif-img/{id}/canvas/c/1", "Canvas id never changed");

        var imageResource = canvas.GetCanvasPaintingBody<Image>();
        imageResource.Id.Should().Be($"{expectedImg}/full/1024,1024/0/default.jpg", "id is jpeg");
        
        var image2 = imageResource.GetService<ImageService2>();
        image2.Id.Should().Be(expectedService2, "ImageService2 is canonical path");
            
        // imageService3 id will be path to a image service. This is canonical so doesn't contain version
        var image3 = imageResource.GetService<ImageService3>();
        image3.Id.Should().Be(expectedService3, "ImageService3 is rewritten as canonical path");
    }
    
    [Theory]
    [InlineData("my-proxy.com", "1", "/const_value/99/|asset|")]
    [InlineData("versioned.com", "2", "/av/_|asset|")]
    [InlineData("non-versioned.com", "3", "/iiif-av/99/|asset|")]
    public async Task Get_V3ManifestForVideo_ReturnsManifest_PathsRewritten_ForTranscode(string hostname, string postfix,
        string expectedRewrite)
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId(assetPostfix: postfix);
        expectedRewrite = $"http://{hostname}{expectedRewrite.Replace("|asset|", id.Asset)}";
        var asset = await dbFixture.DbContext.Images
            .AddTestAsset(id, mediaType: "video/whatever")
            .WithTestDeliveryChannel("iiif-av");
        asset.Entity.WithTestTranscodeMetadata([
            new AVTranscode
            {
                Duration = 10, Width = 20, Height = 30, MediaType = "video/avi",
                Location = new Uri($"s3://dlcs-storage/{id}/full/full/max/max/0/default.avi")
            }
        ]);
        await dbFixture.DbContext.SaveChangesAsync();

        var path = $"iiif-manifest/v3/{id}";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Host", hostname);
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var manifest = (await response.Content.ReadAsStreamAsync()).FromJsonStream<IIIF3.Manifest>();
        manifest.Id.Should().Be($"http://{hostname}/iiif-manifest/v3/{id}", "Manifest id never changed");
        
        var canvas = manifest.Items.Single();
        canvas.Id.Should().Be($"http://{hostname}/iiif-img/{id}/canvas/c/1", "Canvas id never changed");

        var videoResource = canvas.GetCanvasPaintingBody<Video>();
        videoResource.Id.Should().Be($"{expectedRewrite}/full/full/max/max/0/default.avi", "id is transcode");
    }
    
    [Theory]
    [InlineData("my-proxy.com", "1", "/const_value/99/|asset|")]
    [InlineData("versioned.com", "2", "/av/_|asset|")]
    [InlineData("non-versioned.com", "3", "/iiif-av/99/|asset|")]
    public async Task Get_V3ManifestForSound_ReturnsManifest_PathsRewritten_ForTranscode(string hostname, string postfix,
        string expectedRewrite)
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId(assetPostfix: postfix);
        expectedRewrite = $"http://{hostname}{expectedRewrite.Replace("|asset|", id.Asset)}";
        var asset = await dbFixture.DbContext.Images
            .AddTestAsset(id, mediaType: "audio/whatever")
            .WithTestDeliveryChannel("iiif-av");
        asset.Entity.WithTestTranscodeMetadata([
            new AVTranscode
            {
                Duration = 10, Width = 20, Height = 30, MediaType = "audio/avi",
                Location = new Uri($"s3://dlcs-storage/{id}/full/max/default.mp3")
            }
        ]);
        await dbFixture.DbContext.SaveChangesAsync();

        var path = $"iiif-manifest/v3/{id}";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Host", hostname);
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var manifest = (await response.Content.ReadAsStreamAsync()).FromJsonStream<IIIF3.Manifest>();
        manifest.Id.Should().Be($"http://{hostname}/iiif-manifest/v3/{id}", "Manifest id never changed");
        
        var canvas = manifest.Items.Single();
        canvas.Id.Should().Be($"http://{hostname}/iiif-img/{id}/canvas/c/1", "Canvas id never changed");

        var soundResource = canvas.GetCanvasPaintingBody<Sound>();
        soundResource.Id.Should().Be($"{expectedRewrite}/full/max/default.mp3", "id is transcode");
    }
    
    [Theory]
    [InlineData("my-proxy.com", "1", "/const_value/99/|asset|")]
    [InlineData("versioned.com", "2", "/binary/_|asset|")]
    [InlineData("non-versioned.com", "3", "/file/99/|asset|")]
    public async Task Get_V3ManifestForFile_ReturnsManifest_PathsRewritten_ForFile(string hostname, string postfix,
        string expectedRewrite)
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId(assetPostfix: postfix);
        expectedRewrite = $"http://{hostname}{expectedRewrite.Replace("|asset|", id.Asset)}";
        await dbFixture.DbContext.Images
            .AddTestAsset(id, mediaType: "application/pdf")
            .WithTestDeliveryChannel("file");
        await dbFixture.DbContext.SaveChangesAsync();

        var path = $"iiif-manifest/v3/{id}";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Host", hostname);
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var manifest = (await response.Content.ReadAsStreamAsync()).FromJsonStream<IIIF3.Manifest>();
        manifest.Id.Should().Be($"http://{hostname}/iiif-manifest/v3/{id}", "Manifest id never changed");
        
        var canvas = manifest.Items.Single();
        canvas.Id.Should().Be($"http://{hostname}/iiif-img/{id}/canvas/c/1", "Canvas id never changed");

        var rendering = canvas.Rendering.Single();
        rendering.Id.Should().Be(expectedRewrite, "rendering path rewritten");

        var soundResource = canvas.GetCanvasPaintingBody<Image>();
        soundResource.Id.Should().Be($"http://{hostname}/static/dataset/placeholder.png",
            "id is placeholder, never changed");
    }

    /// <summary>
    /// The value provided to tests contains the `v2` slug as that's not the canonical version. The ImageServices output
    /// on the manifest will contain v2 for ImageService2 but won't output a version for ImageService3 as it's canonical.
    /// This method is a helper to get the expected imageService 2+3 paths. 
    /// </summary>
    private static (string expectedService2, string expectedService3) GetImageVersionSpecific(string hostname,
        string expectedVersioned, AssetId assetId)
    {
        var expectedService2 = $"http://{hostname}{expectedVersioned.Replace("|asset|", assetId.Asset)}";
        var expectedService3 = $"http://{hostname}{expectedVersioned
            .Replace("|asset|", assetId.Asset)
            .Replace("v2", string.Empty)
            .Replace("//", "/")}";
        return (expectedService2, expectedService3);
    }
}
