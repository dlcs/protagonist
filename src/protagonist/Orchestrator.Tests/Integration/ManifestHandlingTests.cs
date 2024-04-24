using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using IIIF.Auth.V2;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using IIIF3 = IIIF.Presentation.V3;

namespace Orchestrator.Tests.Integration;

/// <summary>
/// Test of all iiif-manifest handling
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class ManifestHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;
    private JToken imageServices;
    private readonly List<ImageDeliveryChannel> imageDeliveryChannels;

    public ManifestHandlingTests(ProtagonistAppFactory<Startup> factory, DlcsDatabaseFixture databaseFixture)
    {
        dbFixture = databaseFixture;

        httpClient = factory
            .WithTestServices(services =>
            {
                services.AddSingleton<IIIIFAuthBuilder, FakeAuth2Client>();
            })
            .WithConnectionString(dbFixture.ConnectionString)
            .CreateClient();

        var thumbsPolicy = dbFixture.DbContext.DeliveryChannelPolicies.Single(d =>
            d.Channel == AssetDeliveryChannels.Thumbnails && d.Customer == 99);

        imageDeliveryChannels = new()
        {
            new ImageDeliveryChannel
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault,
            },
            new ImageDeliveryChannel
            {
                Channel = AssetDeliveryChannels.Thumbnails,
                DeliveryChannelPolicyId = thumbsPolicy.Id,
            }
        };
        
        dbFixture.CleanUp();
    }

    [Theory]
    [InlineData("iiif-manifest/1/1/my-asset")]
    [InlineData("iiif-manifest/v2/1/1/my-asset")]
    [InlineData("iiif-manifest/v3/1/1/my-asset")]
    public async Task Options_Returns200_WithCorsHeaders(string path)
    {
        // Act
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
        response.Headers.Should().ContainKey("Access-Control-Allow-Headers");
        response.Headers.Should().ContainKey("Access-Control-Allow-Methods");
    }

    [Theory]
    [InlineData("iiif-manifest/1/1/my-asset")]
    [InlineData("iiif-manifest/v2/1/1/my-asset")]
    [InlineData("iiif-manifest/v3/1/1/my-asset")]
    public async Task Get_UnknownCustomer_Returns404(string path)
    {
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
        
    [Theory]
    [InlineData("iiif-manifest/99/5/my-asset")]
    [InlineData("iiif-manifest/v2/99/5/my-asset")]
    [InlineData("iiif-manifest/v3/99/5/my-asset")]
    public async Task Get_UnknownSpace_Returns404(string path)
    {
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
        
    [Theory]
    [InlineData("iiif-manifest/99/1/my-asset")]
    [InlineData("iiif-manifest/v2/99/1/my-asset")]
    [InlineData("iiif-manifest/v3/99/1/my-asset")]
    public async Task Get_UnknownImage_Returns404(string path)
    {
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_NotForDelivery_Returns404()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_NotForDelivery_Returns404)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, notForDelivery: true);
        await dbFixture.DbContext.SaveChangesAsync();
            
        var path = $"iiif-manifest/{id}";

        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
        
    [Theory]
    [InlineData(AssetFamily.File)]
    [InlineData(AssetFamily.Timebased)]
    public async Task Get_NonImage_Returns404(AssetFamily family)
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_NonImage_Returns404)}:{family}");
        await dbFixture.DbContext.Images.AddTestAsset(id, family: family);
        await dbFixture.DbContext.SaveChangesAsync();
            
        var path = $"iiif-manifest/{id}";

        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
        
    [Fact]
    public async Task Get_ManifestForImage_ReturnsManifest_CustomPathRules_Ignored()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_ManifestForImage_ReturnsManifest_CustomPathRules_Ignored)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin", imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();
            
        var path = $"iiif-manifest/v2/{id}";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Host", "my-proxy.com");
        var response = await httpClient.SendAsync(request);
            
        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should().Be($"http://my-proxy.com/iiif-manifest/v2/{id}");
        jsonResponse.SelectToken("sequences[0].canvases[0].thumbnail.@id").Value<string>()
            .Should().StartWith($"http://my-proxy.com/thumbs/{id}/full");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }
    
    [Fact]
    public async Task Get_ManifestForImage_ReturnsManifest()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_ManifestForImage_ReturnsManifest)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin", imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();
            
        var path = $"iiif-manifest/v2/{id}";

        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-manifest/v2/{id}");
        jsonResponse.SelectToken("sequences[0].canvases[0].thumbnail.@id").Value<string>()
            .Should().StartWith($"http://localhost/thumbs/{id}/full");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }
    
    [Fact]
    public async Task Get_V2ManifestForImage_ReturnsManifest_FromMetadata()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_ManifestForImage_ReturnsManifest)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin", imageDeliveryChannels: imageDeliveryChannels)
            .WithTestThumbnailMetadata();
        await dbFixture.DbContext.SaveChangesAsync();
            
        var path = $"iiif-manifest/v2/{id}";

        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-manifest/v2/{id}");
        jsonResponse.SelectToken("sequences[0].canvases[0].thumbnail.@id").Value<string>()
            .Should().StartWith($"http://localhost/thumbs/{id}/full");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }
    
    [Fact]
    public async Task Get_V2ManifestForImage_ReturnsManifestNoThumbnails_WhenNoThumbsChannel()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_ManifestForImage_ReturnsManifest)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin").WithTestThumbnailMetadata();
        await dbFixture.DbContext.SaveChangesAsync();
            
        var path = $"iiif-manifest/v2/{id}";

        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-manifest/v2/{id}");
        jsonResponse.SelectToken("sequences[0].canvases[0].thumbnail").Should().BeNull();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }
    
    [Fact]
    public async Task Get_ManifestForImage_ReturnsManifest_ByName()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_ManifestForImage_ReturnsManifest_ByName)}");
        var namedId = $"test/1/{nameof(Get_ManifestForImage_ReturnsManifest_ByName)}";
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin", imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();
            
        var path = $"iiif-manifest/v2/{namedId}";

        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-manifest/v2/{namedId}");
        jsonResponse.SelectToken("sequences[0].canvases[0].thumbnail.@id").Value<string>()
            .Should().StartWith($"http://localhost/thumbs/{id}/full");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Get_V3ManifestForImage_ReturnsManifest_WithCustomFields()
    {
        // Arrange
        const string defaultLanguage = "none"; 
        var id = AssetId.FromString($"99/1/{nameof(Get_V3ManifestForImage_ReturnsManifest_WithCustomFields)}");
        var namedId = $"test/1/{nameof(Get_V3ManifestForImage_ReturnsManifest_WithCustomFields)}";
        var asset = await dbFixture.DbContext.Images.AddTestAsset(id, 
            origin: "testorigin",
            ref1: "string-example-1",
            ref2: "string-example-2",
            ref3: "string-example-3",
            num1: 1,
            num2: 2,
            num3: 3, 
            imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();
        
        var path = $"iiif-manifest/{namedId}";
        
        // Act
        var response = await httpClient.GetAsync(path);
      
        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        var metadata = jsonResponse.SelectToken("items[0].metadata")
            .ToObject<List<LabelValuePair>>()
            .ToDictionary(
                lvp => lvp.Label[defaultLanguage][0],
                lvp => lvp.Value[defaultLanguage][0]);
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        metadata.Should().NotBeNullOrEmpty();
        metadata.Should().Contain("String 1", asset.Entity.Reference1);
        metadata.Should().Contain("String 2", asset.Entity.Reference2);
        metadata.Should().Contain("String 3", asset.Entity.Reference3);
        metadata.Should().Contain("Number 1", asset.Entity.NumberReference1.ToString());
        metadata.Should().Contain("Number 2", asset.Entity.NumberReference2.ToString());
        metadata.Should().Contain("Number 3", asset.Entity.NumberReference3.ToString());
    }
    
        [Fact]
    public async Task Get_V3ManifestForImage_ReturnsManifest_FromMetadata()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_ManifestForImage_ReturnsManifest)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin", imageDeliveryChannels: imageDeliveryChannels)
            .WithTestThumbnailMetadata();
        await dbFixture.DbContext.SaveChangesAsync();
            
        var path = $"iiif-manifest/{id}";

        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be($"http://localhost/iiif-manifest/{id}");
        jsonResponse.SelectToken("items[0].thumbnail[0].id").Value<string>()
            .Should().StartWith($"http://localhost/thumbs/{id}/full");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }
    
    [Fact]
    public async Task Get_V3ManifestForImage_ReturnsManifestNoThumbnails_WhenNoThumbsChannel()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_ManifestForImage_ReturnsManifest)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin").WithTestThumbnailMetadata();
        await dbFixture.DbContext.SaveChangesAsync();
            
        var path = $"iiif-manifest/{id}";

        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be($"http://localhost/iiif-manifest/{id}");
        jsonResponse.SelectToken("items[0].thumbnail").Should().BeNull();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }
    
    [Fact]
    public async Task Get_V2ManifestForImage_ReturnsManifest_WithCustomFields()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_V2ManifestForImage_ReturnsManifest_WithCustomFields)}");
        var namedId = $"test/1/{nameof(Get_V2ManifestForImage_ReturnsManifest_WithCustomFields)}";
        var asset = await dbFixture.DbContext.Images.AddTestAsset(id, 
            origin: "testorigin",
            ref1: "string-example-1",
            ref2: "string-example-2",
            ref3: "string-example-3",
            num1: 1,
            num2: 2,
            num3: 3, 
            imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();
        
        var path = $"iiif-manifest/v2/{namedId}";
        
        // Act
        var response = await httpClient.GetAsync(path);
      
        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        var metadata = jsonResponse.SelectToken("sequences[0].canvases[0].metadata")
            .ToObject<List<IIIF.Presentation.V2.Metadata>>()
            .ToDictionary(
                m => m.Label.LanguageValues[0].Value,
                m => m.Value.LanguageValues[0].Value);
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        metadata.Should().NotBeNullOrEmpty();
        metadata.Should().Contain("String 1", asset.Entity.Reference1);
        metadata.Should().Contain("String 2", asset.Entity.Reference2);
        metadata.Should().Contain("String 3", asset.Entity.Reference3);
        metadata.Should().Contain("Number 1", asset.Entity.NumberReference1.ToString());
        metadata.Should().Contain("Number 2", asset.Entity.NumberReference2.ToString());
        metadata.Should().Contain("Number 3", asset.Entity.NumberReference3.ToString());
    }
    
    [Fact]
    public async Task Get_V2ManifestForRestrictedImage_ReturnsManifest_WithoutAuthServices()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_V2ManifestForRestrictedImage_ReturnsManifest_WithoutAuthServices)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 400,
            origin: "testorigin", imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();

        var path = $"iiif-manifest/v2/{id}";

        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        var jsonContent = await response.Content.ReadAsStringAsync();
        var jsonResponse = JObject.Parse(jsonContent);
        
        jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-manifest/v2/{id}");
        jsonResponse.SelectToken("sequences[0].canvases[0].thumbnail.@id").Value<string>()
            .Should().StartWith($"http://localhost/thumbs/{id}/full");
        jsonResponse.SelectTokens("sequences[*].canvases[*].images[*].resource.service")
            .Select(token => token.ToString())
            .Should().NotContainMatch("*clickthrough*", "auth services are not included in v2 manifests");
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Get_ReturnsV2Manifest_ViaConneg()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_ReturnsV2Manifest_ViaConneg)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin", imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();
        var path = $"iiif-manifest/{id}";
            
        const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/2/context.json\"";
            
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Accept", iiif2);
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.Vary.Should().Contain("Accept");
        response.Content.Headers.ContentType.ToString().Should().Be(iiif2);
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-manifest/{id}");
        jsonResponse["@context"].ToString().Should().Be("http://iiif.io/api/presentation/2/context.json");
        jsonResponse.SelectToken("sequences[0].canvases").Count().Should().Be(1);
    }
        
    [Fact]
    public async Task Get_ReturnsV2Manifest_ViaDirectPath()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_ReturnsV2Manifest_ViaDirectPath)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin", imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();
        var path = $"iiif-manifest/v2/{id}";
        const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/2/context.json\"";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.Vary.Should().Contain("Accept");
        response.Content.Headers.ContentType.ToString().Should().Be(iiif2);
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-manifest/v2/{id}");
        jsonResponse["@context"].ToString().Should().Be("http://iiif.io/api/presentation/2/context.json");
        jsonResponse.SelectToken("sequences[0].canvases").Count().Should().Be(1);
    }
        
    [Fact]
    public async Task Get_ReturnsV3Manifest_ViaConneg()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_ReturnsV3Manifest_ViaConneg)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin", imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();
        var path = $"iiif-manifest/{id}";
            
        const string iiif3 = "application/ld+json; profile=\"http://iiif.io/api/presentation/3/context.json\"";
            
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Accept", iiif3);
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.Vary.Should().Contain("Accept");
        response.Content.Headers.ContentType.ToString().Should().Be(iiif3);
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be($"http://localhost/iiif-manifest/{id}");
        jsonResponse["@context"].ToString().Should().Be("http://iiif.io/api/presentation/3/context.json");
        jsonResponse.SelectToken("items").Count().Should().Be(1);
    }
        
    [Fact]
    public async Task Get_ReturnsV3Manifest_ViaDirectPath()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_ReturnsV3Manifest_ViaDirectPath)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin", imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();
        var path = $"iiif-manifest/{id}";
        const string iiif3 = "application/ld+json; profile=\"http://iiif.io/api/presentation/3/context.json\"";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.Vary.Should().Contain("Accept");
        response.Content.Headers.ContentType.ToString().Should().Be(iiif3);
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be($"http://localhost/iiif-manifest/{id}");
        jsonResponse["@context"].ToString().Should().Be("http://iiif.io/api/presentation/3/context.json");
        jsonResponse.SelectToken("items").Count().Should().Be(1);
    }
        
    [Fact]
    public async Task Get_ReturnsV3ManifestWithCorrectItemCount_AsCanonical()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_ReturnsV3ManifestWithCorrectItemCount_AsCanonical)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin", imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();
        var path = $"iiif-manifest/{id}";
        const string iiif3 = "application/ld+json; profile=\"http://iiif.io/api/presentation/3/context.json\"";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.Vary.Should().Contain("Accept");
        response.Content.Headers.ContentType.ToString().Should().Be(iiif3);
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@context"].ToString().Should().Be("http://iiif.io/api/presentation/3/context.json");
        jsonResponse.SelectToken("items").Count().Should().Be(1);
    }

    [Fact]
    public async Task Get_ReturnsMultipleImageServices()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_ReturnsMultipleImageServices)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin", imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();
        var path = $"iiif-manifest/{id}";
            
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());

        imageServices = jsonResponse.SelectToken("items[0].items[0].items[0].body.service");
        imageServices.Should().HaveCount(2);
            
        // Image2, non-canonical so Id has version in path
        imageServices.First()["@context"].ToString().Should().Be("http://iiif.io/api/image/2/context.json");
        imageServices.First()["@id"].ToString().Should().Be($"http://localhost/iiif-img/v2/{id}");
                
        // Image3, canonical so Id doesn't have version in path
        imageServices.Last["@context"].ToString().Should().Be("http://iiif.io/api/image/3/context.json");
        imageServices.Last()["id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
    }
    
    [Fact]
    public async Task Get_V3ManifestForRestrictedImage_ReturnsManifest_WithAuthServices()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_V3ManifestForRestrictedImage_ReturnsManifest_WithAuthServices)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 400,
            origin: "testorigin", imageDeliveryChannels: imageDeliveryChannels);
        await dbFixture.DbContext.SaveChangesAsync();

        var path = $"iiif-manifest/v3/{id}";

        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        var manifest = (await response.Content.ReadAsStreamAsync()).FromJsonStream<IIIF3.Manifest>();
        manifest.Context.ToString().Should().Contain("http://iiif.io/api/auth/2/context.json", "Auth2 context added");
        manifest.Services.Should().ContainItemsAssignableTo<AuthAccessService2>()
            .And.HaveCount(1, "item requires auth");
        
        manifest.Id.Should().Be($"http://localhost/iiif-manifest/v3/{id}");
        var paintable = manifest.Items.First()
            .Items.First()
            .Items.Cast<PaintingAnnotation>().Single()
            .Body.As<IIIF.Presentation.V3.Content.Image>();
            
        paintable.Service.Should().HaveCount(2);
        paintable.Service.OfType<ImageService2>().Single().Service.Should()
            .ContainSingle(s => s is AuthProbeService2 && s.Id.Contains(id.ToString()));
        paintable.Service.OfType<ImageService3>().Single().Service.Should()
            .ContainSingle(s => s is AuthProbeService2 && s.Id.Contains(id.ToString()));
    }
}