using System;
using System.Collections.Generic;
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
    [Collection(StorageCollection.CollectionName)]
    public class ManifestHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsDatabaseFixture dbFixture;
        private readonly HttpClient httpClient;
        private readonly IAmazonS3 amazonS3;

        public ManifestHandlingTests(ProtagonistAppFactory<Startup> factory, StorageFixture storageFixture)
        {
            dbFixture = storageFixture.DbFixture;
            amazonS3 = storageFixture.LocalStackFixture.AmazonS3;
            
            httpClient = factory
                .WithConnectionString(dbFixture.ConnectionString)
                .WithLocalStack(storageFixture.LocalStackFixture)
                .CreateClient();
            
            dbFixture.CleanUp();
        }
        
        [Fact]
        public async Task Get_UnknownCustomer_Returns404()
        {
            // Arrange
            const string path = "iiif-manifest/1/1/my-asset";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_UnknownSpace_Returns404()
        {
            // Arrange
            const string path = "iiif-manifest/99/5/my-asset";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_UnknownImage_Returns404()
        {
            // Arrange
            const string path = "iiif-manifest/99/1/my-asset";

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
        public async Task Get_ManifestForImage_HandlesNoAvailableThumbs()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_ManifestForImage_HandlesNoAvailableThumbs)}";
            await dbFixture.DbContext.Images.AddTestAsset(id);
            await dbFixture.DbContext.SaveChangesAsync();
            
            var path = $"iiif-manifest/{id}";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should().Be("http://localhost/iiif-img/99/1/Get_ManifestForImage_HandlesNoAvailableThumbs");
            jsonResponse.SelectToken("sequences[0].canvases[0].thumbnail").Should().BeNull();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.CacheControl.Public.Should().BeTrue();
            response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        }
        
        [Fact]
        public async Task Get_ManifestForImage_ReturnsManifest()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_ManifestForImage_ReturnsManifest)}";
            await dbFixture.DbContext.Images.AddTestAsset(id);
            await dbFixture.DbContext.SaveChangesAsync();

            var openSizes = new List<int[]> { new[] { 100, 100 }, new[] { 200, 200 } };
            await amazonS3.AddSizesJson(id, new ThumbnailSizes(openSizes, null));

            var path = $"iiif-manifest/{id}";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should().Be("http://localhost/iiif-img/99/1/Get_ManifestForImage_ReturnsManifest");
            jsonResponse.SelectToken("sequences[0].canvases[0].thumbnail.@id").Value<string>().Should().Be(
                "http://localhost/thumbs/99/1/Get_ManifestForImage_ReturnsManifest/full/100,100/0/default.jpg");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.CacheControl.Public.Should().BeTrue();
            response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        }
    }
}