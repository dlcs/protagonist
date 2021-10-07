using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using DLCS.Core.Collections;
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
        
        [Fact]
        public async Task Get_ManifestForRestrictedImage_ReturnsManifest_With401Response_IfNoBearerTokenProvided()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_ManifestForRestrictedImage_ReturnsManifest_With401Response_IfNoBearerTokenProvided)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough");
            await dbFixture.DbContext.SaveChangesAsync();

            var openSizes = new List<int[]> { new[] { 100, 100 }, new[] { 200, 200 } };
            await amazonS3.AddSizesJson(id, new ThumbnailSizes(openSizes, null));

            var path = $"iiif-manifest/{id}";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.CacheControl.Private.Should().BeTrue();
        }
        
        [Fact]
        public async Task Get_ManifestForRestrictedImage_ReturnsManifest_With401Response_IfUnknownBearerTokenProvided()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_ManifestForRestrictedImage_ReturnsManifest_With401Response_IfUnknownBearerTokenProvided)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough");
            await dbFixture.DbContext.SaveChangesAsync();

            var openSizes = new List<int[]> { new[] { 100, 100 }, new[] { 200, 200 } };
            await amazonS3.AddSizesJson(id, new ThumbnailSizes(openSizes, null));

            var path = $"iiif-manifest/{id}";

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Authorization", "Bearer __nonsensetoken__");
            var response = await httpClient.SendAsync(request);
            
            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.CacheControl.Private.Should().BeTrue();
        }
        
        [Fact]
        public async Task Get_ManifestForRestrictedImage_ReturnsManifest_With401Response_IfExpiredBearerTokenProvided()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_ManifestForRestrictedImage_ReturnsManifest_With401Response_IfExpiredBearerTokenProvided)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough");
            var userSession =
                await dbFixture.DbContext.SessionUsers.AddTestSession(
                    DlcsDatabaseFixture.ClickThroughAuthService.AsList());
            var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.Now.AddMinutes(-1),
                sessionUserId: userSession.Entity.Id);
            await dbFixture.DbContext.SaveChangesAsync();

            var openSizes = new List<int[]> { new[] { 100, 100 }, new[] { 200, 200 } };
            await amazonS3.AddSizesJson(id, new ThumbnailSizes(openSizes, null));

            var path = $"iiif-manifest/{id}";

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Authorization", $"Bearer {authToken.Entity.BearerToken}");
            var response = await httpClient.SendAsync(request);
            
            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.CacheControl.Private.Should().BeTrue();
        }
        
        [Fact]
        public async Task Get_ManifestForRestrictedImage_ReturnsManifest_With200Response_WithCookie_AndRefreshesToken_IfValidBearerTokenProvided()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_ManifestForRestrictedImage_ReturnsManifest_With200Response_WithCookie_AndRefreshesToken_IfValidBearerTokenProvided)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough");
            var userSession =
                await dbFixture.DbContext.SessionUsers.AddTestSession(
                    DlcsDatabaseFixture.ClickThroughAuthService.AsList());
            var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.Now.AddMinutes(1),
                sessionUserId: userSession.Entity.Id, ttl: 6000, lastChecked: DateTime.Now.AddHours(-1));
            await dbFixture.DbContext.SaveChangesAsync();

            var openSizes = new List<int[]> { new[] { 100, 100 }, new[] { 200, 200 } };
            await amazonS3.AddSizesJson(id, new ThumbnailSizes(openSizes, null));

            var path = $"iiif-manifest/{id}";
            
            var bearerToken = authToken.Entity.BearerToken;

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Authorization", $"Bearer {bearerToken}");
            var response = await httpClient.SendAsync(request);
            
            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.CacheControl.Private.Should().BeTrue();

            response.Headers.Should().ContainKey("Set-Cookie");

            dbFixture.DbContext.AuthTokens.Single(t => t.BearerToken == bearerToken)
                .Expires.Should().BeAfter(DateTime.Now.AddMinutes(5));
        }
    }
}