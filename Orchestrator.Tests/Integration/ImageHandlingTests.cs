using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Auth;
using DLCS.Model.Auth.Entities;
using DLCS.Repository.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Orchestrator.Assets;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Tests.Integration
{
    /// <summary>
    /// Test of all requests handled by custom iiif-img handling
    /// </summary>
    [Trait("Category", "Integration")]
    [Collection(StorageCollection.CollectionName)]
    public class ImageHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsDatabaseFixture dbFixture;
        private readonly HttpClient httpClient;
        private readonly IAmazonS3 amazonS3;
        private readonly FakeImageOrchestrator orchestrator = new();

        public ImageHandlingTests(ProtagonistAppFactory<Startup> factory, StorageFixture storageFixture)
        {
            dbFixture = storageFixture.DbFixture;
            amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
            httpClient = factory
                .WithConnectionString(dbFixture.ConnectionString)
                .WithLocalStack(storageFixture.LocalStackFixture)    
                .WithTestServices(services =>
                {
                    services
                        .AddSingleton<IForwarderHttpClientFactory, TestProxyHttpClientFactory>()
                        .AddSingleton<IHttpForwarder, TestProxyForwarder>()
                        .AddSingleton<IImageOrchestrator>(orchestrator)
                        .AddSingleton<TestProxyHandler>();
                })
                .CreateClient(new WebApplicationFactoryClientOptions {AllowAutoRedirect = false});
            
            dbFixture.CleanUp();
        }
        
        [Theory]
        [InlineData("/iiif-img/2/1/image")]
        [InlineData("/iiif-img/2/1/image/")]
        [InlineData("/iiif-img/display-name/1/image")]
        [InlineData("/iiif-img/display-name/1/image/")]
        [InlineData("/iiif-img/v2/2/1/image")]
        [InlineData("/iiif-img/v2/2/1/image/")]
        [InlineData("/iiif-img/v2/display-name/1/image")]
        [InlineData("/iiif-img/v2/display-name/1/image/")]
        [InlineData("/iiif-img/v3/2/1/image")]
        [InlineData("/iiif-img/v3/2/1/image/")]
        [InlineData("/iiif-img/v3/display-name/1/image")]
        [InlineData("/iiif-img/v3/display-name/1/image/")]
        public async Task Get_ImageRoot_RedirectsToInfoJson(string path)
        {
            // Arrange
            var expected = path[^1] == '/' ? $"{path}info.json" : $"{path}/info.json";
            
            // Act
            var response = await httpClient.GetAsync(path);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
            response.Headers.Location.Should().Be(expected);
        }
        
        [Theory]
        [InlineData("/iiif-img/v21/2/1/image")]
        [InlineData("/iiif-img/v2.1/2/1/image/")]
        public async Task Get_ImageRoot_404_IfIncorrectVersionSlugProvided(string path)
        {
            // Act
            var response = await httpClient.GetAsync(path);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetInfoJson_OpenImage_Correct()
        {
            // Arrange
            var id = $"99/1/{nameof(GetInfoJson_OpenImage_Correct)}";
            await dbFixture.DbContext.Images.AddTestAsset(id);

            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[800,800],[400,400],[200,200]]}"
            });
            await dbFixture.DbContext.SaveChangesAsync();
            
            // Act
            var response = await httpClient.GetAsync($"iiif-img/{id}/info.json");

            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should().Be("http://localhost/iiif-img/99/1/GetInfoJson_OpenImage_Correct");
            jsonResponse["height"].ToString().Should().Be("8000");
            jsonResponse["width"].ToString().Should().Be("8000");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.CacheControl.Public.Should().BeTrue();
            response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        }
        
        [Fact]
        public async Task GetInfoJson_OrchestratesImage()
        {
            // Arrange
            var id = $"99/1/{nameof(GetInfoJson_OrchestratesImage)}";
            await dbFixture.DbContext.Images.AddTestAsset(id);

            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[800,800],[400,400],[200,200]]}"
            });
            await dbFixture.DbContext.SaveChangesAsync();
            
            // Act
            await httpClient.GetAsync($"iiif-img/{id}/info.json");

            // Assert
            FakeImageOrchestrator.OrchestratedImages.Should().Contain(AssetId.FromString(id));
        }
        
        [Fact]
        public async Task GetInfoJson_DoesNotOrchestratesImage_IfQueryParamPassed()
        {
            // Arrange
            var id = $"99/1/{nameof(GetInfoJson_DoesNotOrchestratesImage_IfQueryParamPassed)}";
            await dbFixture.DbContext.Images.AddTestAsset(id);

            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[800,800],[400,400],[200,200]]}"
            });
            await dbFixture.DbContext.SaveChangesAsync();
            
            // Act
            await httpClient.GetAsync($"iiif-img/{id}/info.json?noOrchestrate=true");

            // Assert
            FakeImageOrchestrator.OrchestratedImages.Should().NotContain(AssetId.FromString(id));
        }
        
        [Fact]
        public async Task GetInfoJson_OpenImage_ForwardedFor_Correct()
        {
            // Arrange
            var id = $"99/1/{nameof(GetInfoJson_OpenImage_ForwardedFor_Correct)}";
            await dbFixture.DbContext.Images.AddTestAsset(id);

            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[800,800],[400,400],[200,200]]}"
            });
            await dbFixture.DbContext.SaveChangesAsync();
            
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/{id}/info.json");
            request.Headers.Add("X-Forwarded-Host", "new-host.dlcs");
            var response = await httpClient.SendAsync(request);

            // Assert
            // TODO - improve these tests when we have IIIF models
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should()
                .Be("http://new-host.dlcs/iiif-img/99/1/GetInfoJson_OpenImage_ForwardedFor_Correct");
            jsonResponse["height"].ToString().Should().Be("8000");
            jsonResponse["width"].ToString().Should().Be("8000");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.CacheControl.Public.Should().BeTrue();
            response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        }
        
        [Fact]
        public async Task GetInfoJson_RestrictedImage_Correct()
        {
            // Arrange
            var id = $"99/1/{nameof(GetInfoJson_RestrictedImage_Correct)}";
            const string roleName = "my-test-role";
            const string authServiceName = "my-auth-service";
            await dbFixture.DbContext.Images.AddTestAsset(id, roles: roleName, maxUnauthorised: 500);
            await dbFixture.DbContext.Roles.AddAsync(new Role
            {
                Customer = 99, Id = roleName, Name = "test-role", AuthService = authServiceName
            });
            await dbFixture.DbContext.AuthServices.AddAsync(new AuthService
            {
                Name = "test-service", Customer = 99, Id = authServiceName, Profile = "profile"
            });

            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[400,400],[200,200]]}"
            });
            await dbFixture.DbContext.SaveChangesAsync();
            
            // Act
            var response = await httpClient.GetAsync($"iiif-img/{id}/info.json");

            // Assert
            // TODO - improve these tests when we have IIIF models
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should()
                .Be("http://localhost/iiif-img/99/1/GetInfoJson_RestrictedImage_Correct");
            jsonResponse["service"].Should().NotBeNull();
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.CacheControl.Public.Should().BeFalse();
            response.Headers.CacheControl.Private.Should().BeTrue();
            response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        }
        
        [Fact]
        public async Task GetInfoJson_RestrictedImage_WithUnknownRole_Returns401WithoutServices()
        {
            // Arrange
            var id = $"99/1/{nameof(GetInfoJson_RestrictedImage_WithUnknownRole_Returns401WithoutServices)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, roles: "unknown-role", maxUnauthorised: 500);

            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[400,400],[200,200]]}"
            });
            await dbFixture.DbContext.SaveChangesAsync();
            
            // Act
            var response = await httpClient.GetAsync($"iiif-img/{id}/info.json");

            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
            jsonResponse["services"].Should().BeNull();
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.CacheControl.Public.Should().BeFalse();
            response.Headers.CacheControl.Private.Should().BeTrue();
            response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        }
        
        [Fact]
        public async Task GetInfoJson_RestrictedImage_WithUnknownRole_Returns401_IfNoBearerTokenProvided()
        {
            // Arrange
            var id = $"99/1/{nameof(GetInfoJson_RestrictedImage_WithUnknownRole_Returns401_IfNoBearerTokenProvided)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 500);
            await dbFixture.DbContext.SaveChangesAsync();
            
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[800,800],[400,400],[200,200]]}"
            });
            
            // Act
            var response = await httpClient.GetAsync($"iiif-img/{id}/info.json");

            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.CacheControl.Public.Should().BeFalse();
            response.Headers.CacheControl.Private.Should().BeTrue();
        }
        
        [Fact]
        public async Task GetInfoJson_RestrictedImage_WithUnknownRole_Returns401_IfUnknownBearerTokenProvided()
        {
            // Arrange
            var id = $"99/1/{nameof(GetInfoJson_RestrictedImage_WithUnknownRole_Returns401_IfUnknownBearerTokenProvided)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 500);
            await dbFixture.DbContext.SaveChangesAsync();
            
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[800,800],[400,400],[200,200]]}"
            });
            
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/{id}/info.json");
            request.Headers.Add("Authorization", "Bearer __nonsensetoken__");
            var response = await httpClient.SendAsync(request);

            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.CacheControl.Public.Should().BeFalse();
            response.Headers.CacheControl.Private.Should().BeTrue();
        }
        
        [Fact]
        public async Task GetInfoJson_RestrictedImage_WithUnknownRole_Returns401_IfExpiredBearerTokenProvided()
        {
            // Arrange
            var id = $"99/1/{nameof(GetInfoJson_RestrictedImage_WithUnknownRole_Returns401_IfExpiredBearerTokenProvided)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 500);
            var userSession =
                await dbFixture.DbContext.SessionUsers.AddTestSession(
                    DlcsDatabaseFixture.ClickThroughAuthService.AsList());
            var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(-1),
                sessionUserId: userSession.Entity.Id);
            await dbFixture.DbContext.SaveChangesAsync();
            
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[800,800],[400,400],[200,200]]}"
            });
            
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/{id}/info.json");
            request.Headers.Add("Authorization", $"Bearer {authToken.Entity.BearerToken}");
            var response = await httpClient.SendAsync(request);

            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.CacheControl.Public.Should().BeFalse();
            response.Headers.CacheControl.Private.Should().BeTrue();
        }
        
        [Fact]
        public async Task GetInfoJson_RestrictedImage_WithUnknownRole_Returns200_AndRefreshesToken_IfValidBearerTokenProvided()
        {
            // Arrange
            var id = $"99/1/{nameof(GetInfoJson_RestrictedImage_WithUnknownRole_Returns200_AndRefreshesToken_IfValidBearerTokenProvided)}";
            await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 500);
            var userSession =
                await dbFixture.DbContext.SessionUsers.AddTestSession(
                    DlcsDatabaseFixture.ClickThroughAuthService.AsList());
            var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(1),
                sessionUserId: userSession.Entity.Id, ttl: 6000, lastChecked: DateTime.UtcNow.AddHours(-1));
            await dbFixture.DbContext.SaveChangesAsync();
            
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[800,800],[400,400],[200,200]]}"
            });
            
            var bearerToken = authToken.Entity.BearerToken;
            
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/{id}/info.json");
            request.Headers.Add("Authorization", $"Bearer {bearerToken}");
            var response = await httpClient.SendAsync(request);

            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.CacheControl.Public.Should().BeFalse();
            response.Headers.CacheControl.Private.Should().BeTrue();
            
            dbFixture.DbContext.AuthTokens.Single(t => t.BearerToken == bearerToken)
                .Expires.Should().BeAfter(DateTime.UtcNow.AddMinutes(5));
        }

        [Fact]
        public async Task Get_UnknownCustomer_Returns404()
        {
            // Arrange
            const string path = "iiif-img/1/1/my-image/full/full/0/default.jpg";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_UnknownSpace_Returns404()
        {
            // Arrange
            const string path = "iiif-img/99/5/my-image/full/full/0/default.jpg";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_UnknownImage_Returns404()
        {
            // Arrange
            const string path = "iiif-img/99/1/my-image/full/full/0/default.jpg";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Theory]
        [InlineData("iiif-img/99/1/my-image/full/all/0/default.jpg")]
        [InlineData("iiif-img/99/1/my-image/!200,200/full/0/default.jpg")]
        public async Task Get_MalformedImageRequest_Returns400(string path)
        {
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        
        [Theory]
        [InlineData("iiif-img/99/1/test-auth-nocookid/full/!200,200/0/default.jpg", "id")]
        [InlineData("iiif-img/test/1/test-auth-nocookdisplay/full/!200,200/0/default.jpg", "display")]
        public async Task Get_ImageRequiresAuth_Returns401_IfNoCookie(string path, string type)
        {
            // Arrange
            await dbFixture.DbContext.Images.AddTestAsset($"99/1/test-auth-nocook{type}", roles: "basic",
                maxUnauthorised: 100);
            await dbFixture.DbContext.SaveChangesAsync();
            
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        
        [Theory]
        [InlineData("iiif-img/99/1/test-auth-invalidcookid/full/!200,200/0/default.jpg", "id")]
        [InlineData("iiif-img/test/1/test-auth-invalidcookdisplay/full/!200,200/0/default.jpg", "display")]
        public async Task Get_ImageRequiresAuth_Returns401_IfInvalidCookie(string path, string type)
        {
            // Arrange
            await dbFixture.DbContext.Images.AddTestAsset($"99/1/test-auth-invalidcook{type}", roles: "basic",
                maxUnauthorised: 100);
            await dbFixture.DbContext.SaveChangesAsync();
            
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Cookie", "dlcs-token-99=blabla;");
            var response = await httpClient.SendAsync(request);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        
        [Theory]
        [InlineData("iiif-img/99/1/test-auth-expcookid/full/!200,200/0/default.jpg", "id")]
        [InlineData("iiif-img/test/1/test-auth-expcookdisplay/full/!200,200/0/default.jpg", "display")]
        public async Task Get_ImageRequiresAuth_Returns401_IfExpiredCookie(string path, string type)
        {
            // Arrange
            var id = $"99/1/test-auth-expcook{type}";
            await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 100);
            var userSession =
                await dbFixture.DbContext.SessionUsers.AddTestSession(
                    DlcsDatabaseFixture.ClickThroughAuthService.AsList());
            var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(-1),
                sessionUserId: userSession.Entity.Id);
            await dbFixture.DbContext.SaveChangesAsync();
            
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
            var response = await httpClient.SendAsync(request);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        
        [Theory]
        [InlineData("iiif-img/99/1/test-auth-cookid/full/max/0/default.jpg", "id")]
        [InlineData("iiif-img/test/1/test-auth-cookdisplay/full/max/0/default.jpg", "display")]
        public async Task Get_ImageRequiresAuth_RedirectsToImageServer_AndSetsCookie_IfCookieProvided(string path, string type)
        {
            // Arrange
            var id = $"99/1/test-auth-cook{type}";
            await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 100);
            var userSession =
                await dbFixture.DbContext.SessionUsers.AddTestSession(
                    DlcsDatabaseFixture.ClickThroughAuthService.AsList());
            var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
                sessionUserId: userSession.Entity.Id);
            await dbFixture.DbContext.SaveChangesAsync();
            
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[400,400], [200,200]]}",
            });
            
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
            var response = await httpClient.SendAsync(request);
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.ToString().Should().StartWith("http://image-server/iiif");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.Should().ContainKey("Set-Cookie");
        }
        
        [Fact]
        public async Task Get_ImageIsExactThumbMatch_RedirectsToThumbs()
        {
            // Arrange
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = "99/1/known-thumb/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[200,200]]}",
            });

            await dbFixture.DbContext.Images.AddTestAsset("99/1/known-thumb", origin: "/test/space", width: 1000,
                height: 1000);
            await dbFixture.DbContext.SaveChangesAsync();
            var expectedPath = new Uri("http://thumbs/thumbs/99/1/known-thumb/full/!200,200/0/default.jpg");
            
            // Act
            var response = await httpClient.GetAsync("iiif-img/99/1/known-thumb/full/!200,200/0/default.jpg");
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
        }

        [Fact]
        public async Task Get_FullRegion_LargerThumbExists_RedirectsToResizeThumbs()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_FullRegion_LargerThumbExists_RedirectsToResizeThumbs)}";
            
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[400,400], [200,200]]}",
            });
            await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000);
            await dbFixture.DbContext.SaveChangesAsync();
            var expectedPath = new Uri($"http://thumbresize/thumbs/{id}/full/!123,123/0/default.jpg");
            
            // Act
            var response = await httpClient.GetAsync($"iiif-img/{id}/full/!123,123/0/default.jpg");
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
        }
        
        [Fact]
        public async Task Get_FullRegion_SmallerThumbExists_NoMatchingUpscaleConfig_RedirectsToImageServer()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_FullRegion_SmallerThumbExists_NoMatchingUpscaleConfig_RedirectsToImageServer)}";
            
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[400,400], [200,200]]}",
            });
            await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000);
            await dbFixture.DbContext.SaveChangesAsync();
            
            // Act
            var response = await httpClient.GetAsync($"iiif-img/{id}/full/!800,800/0/default.jpg");
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.ToString().Should().StartWith("http://image-server/iiif");
        }
        
        [Fact]
        public async Task Get_FullRegion_NoOpenThumbs_RedirectsToImageServer()
        {
            // Arrange
            var id = $"99/1/{nameof(Get_FullRegion_NoOpenThumbs_RedirectsToImageServer)}";
            
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": []}",
            });

            await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000);
            await dbFixture.DbContext.SaveChangesAsync();
            
            // Act
            var response = await httpClient.GetAsync($"iiif-img/{id}/full/!800,800/0/default.jpg");
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.ToString().Should().StartWith("http://image-server/iiif");
        }
        
        [Fact]
        public async Task Get_FullRegion_HasSmallerThumb_MatchesUpscaleRegex_ThresholdTooLarge_RedirectsToImageServer()
        {
            // Arrange
            var id = $"99/1/upscale{nameof(Get_FullRegion_HasSmallerThumb_MatchesUpscaleRegex_ThresholdTooLarge_RedirectsToImageServer)}";
            
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[300,300]]}",
            });

            await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000);
            await dbFixture.DbContext.SaveChangesAsync();
            
            // Act
            var response = await httpClient.GetAsync($"iiif-img/{id}/full/!800,800/0/default.jpg");
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.ToString().Should().StartWith("http://image-server/iiif");
        }
        
        [Fact]
        public async Task Get_FullRegion_HasSmallerThumb_MatchesUpscaleRegex_WithinThreshold_RedirectsToResizeThumbs()
        {
            // Arrange
            var id = $"99/1/upscale{nameof(Get_FullRegion_HasSmallerThumb_MatchesUpscaleRegex_WithinThreshold_RedirectsToResizeThumbs)}";
            
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"{id}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[300,300]]}",
            });

            await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000);
            await dbFixture.DbContext.SaveChangesAsync();
            var expectedPath = new Uri($"http://thumbresize/thumbs/{id}/full/!600,600/0/default.jpg");
            
            // Act
            var response = await httpClient.GetAsync($"iiif-img/{id}/full/!600,600/0/default.jpg");
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
        }
        
        [Fact]
        public async Task Get_Thumbs_RedirectsToThumbs()
        {
            // Arrange
            var expectedPath = new Uri("http://thumbs/thumbs/99/1/known-thumb/full/!200,200/0/default.jpg");
            
            // Act
            var response = await httpClient.GetAsync("thumbs/99/1/known-thumb/full/!200,200/0/default.jpg");
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
        }
        
        [Theory]
        [InlineData("iiif-img/99/1/resize/full/!200,200/0/default.jpg", "resize")]
        [InlineData("iiif-img/99/1/full/full/full/0/default.jpg", "full")]
        [InlineData("iiif-img/99/1/tile/0,0,1000,1000/200,200/0/default.jpg", "tile")]
        [InlineData("iiif-img/test/1/rewrite_id/0,0,1000,1000/200,200/0/default.jpg", "rewrite_id", "iiif-img/99/1/rewrite_id/0,0,1000,1000/200,200/0/default.jpg")]
        public async Task Get_RedirectsImageServer_AsFallThrough(string path, string imageName, string rewrittenPath = null)
        {
            // Arrange
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"99/1/{imageName}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": []}",
            });

            await dbFixture.DbContext.Images.AddTestAsset($"99/1/{imageName}", origin: "/test/space", width: 1000,
                height: 1000);
            await dbFixture.DbContext.CustomHeaders.AddTestCustomHeader("x-test-key", "foo bar");
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync(path);
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.ToString().Should().StartWith("http://image-server/iiif");
            response.Headers.CacheControl.Public.Should().BeTrue();
            response.Headers.CacheControl.SharedMaxAge.Should().Be(TimeSpan.FromDays(28));
            response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromDays(28));
            response.Headers.Should().ContainKey("x-test-key").WhoseValue.Should().BeEquivalentTo("foo bar");
        }
    }
    
    public class FakeImageOrchestrator : IImageOrchestrator
    {
        public static List<AssetId> OrchestratedImages { get; } = new();

        public Task OrchestrateImage(OrchestrationImage orchestrationImage,
            CancellationToken cancellationToken = default)
        {
            OrchestratedImages.Add(orchestrationImage.AssetId);
            return Task.CompletedTask;
        }
    }
}