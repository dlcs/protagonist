using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using Xunit;

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

    public ManifestHandlingTests(ProtagonistAppFactory<Startup> factory, DlcsDatabaseFixture databaseFixture)
    {
        dbFixture = databaseFixture;

        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .CreateClient();
            
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
        var id = $"99/1/{nameof(Get_NotForDelivery_Returns404)}";
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
        var id = $"99/1/{nameof(Get_NonImage_Returns404)}:{family}";
        await dbFixture.DbContext.Images.AddTestAsset(id, family: family);
        await dbFixture.DbContext.SaveChangesAsync();
            
        var path = $"iiif-manifest/{id}";

        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
        
    [Fact]
    public async Task Get_ManifestForImage_ReturnsManifest()
    {
        // Arrange
        var id = $"99/1/{nameof(Get_ManifestForImage_ReturnsManifest)}";
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin");
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
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }
        
    [Fact]
    public async Task Get_ManifestForRestrictedImage_ReturnsManifest()
    {
        // Arrange
        var id = $"99/1/{nameof(Get_ManifestForRestrictedImage_ReturnsManifest)}";
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 400,
            origin: "testorigin");
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
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Get_ReturnsV2Manifest_ViaConneg()
    {
        // Arrange
        var id = $"99/1/{nameof(Get_ReturnsV2Manifest_ViaConneg)}";
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin");
        await dbFixture.DbContext.SaveChangesAsync();
        var path = $"iiif-manifest/{id}";
            
        const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/2/context.json\"";
            
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Accept", iiif2);
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        var id = $"99/1/{nameof(Get_ReturnsV2Manifest_ViaDirectPath)}";
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin");
        await dbFixture.DbContext.SaveChangesAsync();
        var path = $"iiif-manifest/v2/{id}";
        const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/2/context.json\"";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        var id = $"99/1/{nameof(Get_ReturnsV3Manifest_ViaConneg)}";
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin");
        await dbFixture.DbContext.SaveChangesAsync();
        var path = $"iiif-manifest/{id}";
            
        const string iiif3 = "application/ld+json; profile=\"http://iiif.io/api/presentation/3/context.json\"";
            
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Accept", iiif3);
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        var id = $"99/1/{nameof(Get_ReturnsV3Manifest_ViaDirectPath)}";
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin");
        await dbFixture.DbContext.SaveChangesAsync();
        var path = $"iiif-manifest/{id}";
        const string iiif3 = "application/ld+json; profile=\"http://iiif.io/api/presentation/3/context.json\"";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        var id = $"99/1/{nameof(Get_ReturnsV3ManifestWithCorrectItemCount_AsCanonical)}";
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin");
        await dbFixture.DbContext.SaveChangesAsync();
        var path = $"iiif-manifest/{id}";
        const string iiif3 = "application/ld+json; profile=\"http://iiif.io/api/presentation/3/context.json\"";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        var id = $"99/1/{nameof(Get_ReturnsMultipleImageServices)}";
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "testorigin");
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
}