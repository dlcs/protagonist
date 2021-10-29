using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using DLCS.Model.Assets;
using DLCS.Repository.Assets;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using Xunit;

namespace Orchestrator.Tests.Integration
{
    /// <summary>
    /// Test of all iiif-manifest handling
    /// </summary>
    [Trait("Category", "Integration")]
    [Collection(DatabaseCollection.CollectionName)]
    public class ManifestHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsDatabaseFixture dbFixture;
        private readonly HttpClient httpClient;

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
            jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
            jsonResponse.SelectToken("sequences[0].canvases[0].thumbnail.@id").Value<string>()
                .Should().Be($"http://localhost/thumbs/{id}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.CacheControl.Public.Should().BeTrue();
            response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        }
        
        [Fact]
        public async Task Get_ManifestForRestrictedImage_ReturnsManifest()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_ManifestForRestrictedImage_ReturnsManifest)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", origin: "testorigin");
            await dbFixture.DbContext.SaveChangesAsync();

            var path = $"iiif-manifest/v2/{id}";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
            jsonResponse.SelectToken("sequences[0].canvases[0].thumbnail.@id").Value<string>()
                .Should().Be($"http://localhost/thumbs/{id}");

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
            jsonResponse["@context"].ToString().Should().Be("http://iiif.io/api/presentation/3/context.json");
            jsonResponse.SelectToken("items").Count().Should().Be(1);
        }
        
        [Fact]
        public async Task Get_ReturnsV3ManifestWithCorrectCount_AsCanonical()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_ReturnsV3ManifestWithCorrectCount_AsCanonical)}";
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
    }
}