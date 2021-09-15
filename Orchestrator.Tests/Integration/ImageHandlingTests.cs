using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Core.Types;
using DLCS.Model.Security;
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
            amazonS3 = storageFixture.LocalStackFixture.AmazonS3;
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
            jsonResponse["services"].Should().NotBeNull();
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.CacheControl.Public.Should().BeFalse();
            response.Headers.CacheControl.Private.Should().BeTrue();
            response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        }
        
        [Fact]
        public async Task GetInfoJson_RestrictedImage_WithUnknownRole_Returns403WithoutServices()
        {
            // Arrange
            var id = $"99/1/{nameof(GetInfoJson_RestrictedImage_WithUnknownRole_Returns403WithoutServices)}";
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
        [InlineData("iiif-img/99/1/test-authid/full/!200,200/0/default.jpg", "id")]
        [InlineData("iiif-img/test/1/test-authdisplay/full/!200,200/0/default.jpg", "display")]
        public async Task Get_ImageRequiresAuth_RedirectsToDeliverator(string path, string type)
        {
            // Arrange
            await dbFixture.DbContext.Images.AddTestAsset($"99/1/test-auth{type}", roles: "basic", maxUnauthorised: 100);
            await dbFixture.DbContext.SaveChangesAsync();
            var expectedPath = new Uri($"http://deliverator/iiif-img/99/1/test-auth{type}/full/!200,200/0/default.jpg");
            
            // Act
            var response = await httpClient.GetAsync(path);
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
        }

        [Fact]
        public async Task Get_ImageIsUVThumb_RewritesSizeAndRedirectsToThumbs()
        {
            // Arrange
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = "99/1/test-uv-thumb/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[200,200]]}",
            });
            await dbFixture.DbContext.Images.AddTestAsset("99/1/test-uv-thumb", origin: "/test/space");
            await dbFixture.DbContext.SaveChangesAsync();
            var expectedPath = new Uri("http://thumbs/thumbs/99/1/test-uv-thumb/full/!200,200/0/default.jpg");
            
            // Act
            var response = await httpClient.GetAsync("iiif-img/99/1/test-uv-thumb/full/90,/0/default.jpg?t=1234");
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
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
            proxyResponse.Uri.ToString().Should().StartWith("http://image-server/fcgi-bin/iipsrv.fcgi?IIIF");
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
            proxyResponse.Uri.ToString().Should().StartWith("http://image-server/fcgi-bin/iipsrv.fcgi?IIIF");
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
            proxyResponse.Uri.ToString().Should().StartWith("http://image-server/fcgi-bin/iipsrv.fcgi?IIIF");
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
            await dbFixture.DbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.GetAsync(path);
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.ToString().Should().StartWith("http://image-server/fcgi-bin/iipsrv.fcgi?IIIF");
            response.Headers.CacheControl.Public.Should().BeTrue();
            response.Headers.CacheControl.SharedMaxAge.Should().Be(TimeSpan.FromDays(28));
            response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromDays(28));
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