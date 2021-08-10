using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Model.Assets;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Integration;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Tests.Integration.Infrastructure
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
                        .AddSingleton<TestProxyHandler>();
                })
                .CreateClient(new WebApplicationFactoryClientOptions {AllowAutoRedirect = false});
            
            dbFixture.CleanUp();
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
        
        [Fact]
        public async Task Get_ImageRequiresAuth_RedirectsToDeliverator()
        {
            // Arrange
            await dbFixture.DbContext.Images.AddAsync(new Asset
            {
                Created = DateTime.Now, Customer = 99, Space = 1, Id = "99/1/test-auth", Roles = "basic",
                MaxUnauthorised = 100, Origin = "/test/space", Family = 'I', MediaType = "image/jpeg",
                ThumbnailPolicy = "default"
            });
            await dbFixture.DbContext.SaveChangesAsync();
            var expectedPath = new Uri("http://deliverator/iiif-img/99/1/test-auth/full/!200,200/0/default.jpg");
            
            // Act
            var response = await httpClient.GetAsync("iiif-img/99/1/test-auth/full/!200,200/0/default.jpg");
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
        }
        
        [Fact]
        public async Task Get_ImageIsUVThumb_RewritesSizeAndRedirectsToThumbs()
        {
            // Arrange
            await dbFixture.DbContext.Images.AddAsync(new Asset
            {
                Created = DateTime.Now, Customer = 99, Space = 1, Id = "99/1/test-uv-thumb",
                Origin = "/test/space", Family = 'I', MediaType = "image/jpeg", ThumbnailPolicy = "default"
            });
            await dbFixture.DbContext.SaveChangesAsync();
            var expectedPath = new Uri("http://thumbs/thumbs/99/1/test-uv-thumb/full/!200,200/0/default.jpg");
            
            // Act
            var response = await httpClient.GetAsync("iiif-img/99/1/test-uv-thumb/full/90,/0/default.jpg?t=1234");
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
        }
        
        [Fact]
        public async Task Get_ImageIsKnownThumb_RedirectsToThumbs()
        {
            // Arrange
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = "99/1/known-thumb/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": [[200,200]]}",
            });

            await dbFixture.DbContext.Images.AddAsync(new Asset
            {
                Created = DateTime.Now, Customer = 99, Space = 1, Id = "99/1/known-thumb", Width = 1000,
                Height = 1000, Origin = "/test/space", Family = 'I', MediaType = "image/jpeg",
                ThumbnailPolicy = "default"
            });
            await dbFixture.DbContext.SaveChangesAsync();
            var expectedPath = new Uri("http://thumbs/thumbs/99/1/known-thumb/full/!200,200/0/default.jpg");
            
            // Act
            var response = await httpClient.GetAsync("iiif-img/99/1/known-thumb/full/!200,200/0/default.jpg");
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
        }
        
        [Theory]
        [InlineData("iiif-img/99/1/resize/full/!200,200/0/default.jpg", "resize")]
        [InlineData("iiif-img/99/1/full/full/full/0/default.jpg", "full")]
        [InlineData("iiif-img/99/1/tile/0,0,1000,1000/200,200/0/default.jpg", "tile")]
        public async Task Get_RedirectsToVarnish_AsFallThrough(string path, string imageName)
        {
            // Arrange
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = $"99/1/{imageName}/s.json",
                BucketName = "protagonist-thumbs",
                ContentBody = "{\"o\": []}",
            });
            
            await dbFixture.DbContext.Images.AddAsync(new Asset
            {
                Created = DateTime.Now, Customer = 99, Space = 1, Id = $"99/1/{imageName}", Width = 1000,
                Height = 1000, Origin = "/test/space", Family = 'I', MediaType = "image/jpeg",
                ThumbnailPolicy = "default"
            });
            await dbFixture.DbContext.SaveChangesAsync();
            var expectedPath = new Uri($"http://varnish/{path}");
            
            // Act
            var response = await httpClient.GetAsync(path);
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
            
            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
        }
    }
}