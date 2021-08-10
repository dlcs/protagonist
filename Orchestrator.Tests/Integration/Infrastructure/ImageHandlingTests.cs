using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
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
    [Collection(DatabaseCollection.CollectionName)]
    public class ImageHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsDatabaseFixture dbFixture;
        private readonly HttpClient httpClient;

        public ImageHandlingTests(ProtagonistAppFactory<Startup> factory, DlcsDatabaseFixture dbFixture)
        {
            this.dbFixture = dbFixture;
            httpClient = factory
                .WithConnectionString(dbFixture.ConnectionString)
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
    }
}