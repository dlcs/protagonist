using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Tests.Integration
{
    /// <summary>
    /// Test of all requests handled by YARP configuration
    /// </summary>
    public class ImageProxyTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly HttpClient httpClient;

        public ImageProxyTests(ProtagonistAppFactory<Startup> factory)
        {
            httpClient = factory
                .WithTestServices(services =>
                {
                    services.AddSingleton<IForwarderHttpClientFactory, TestProxyHttpClientFactory>();
                    services.AddSingleton<TestProxyHandler>();
                })
                .CreateClient();
        }
        
        [Fact]
        public async Task Test_OptionsRequest_RedirectsToDeliverator()
        {
            // Arrange
            var message = new HttpRequestMessage(HttpMethod.Options, "http://deliverator/iiif-img/2/1/image");
            var expectedPath = new Uri("http://deliverator/iiif-img/2/1/image");
            
            // Act
            var response = await httpClient.SendAsync(message);
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();

            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
            proxyResponse.Method.Should().Be(HttpMethod.Options);
        }
        
        [Fact]
        public async Task Test_ImageGetRoot_RedirectsToDeliverator()
        {
            // Arrange
            var expectedPath = new Uri("http://deliverator/iiif-img/2/1/image");
            
            // Act
            var response = await httpClient.GetAsync("/iiif-img/2/1/image");
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();

            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
            proxyResponse.Method.Should().Be(HttpMethod.Get);
        }
        
        [Fact]
        public async Task Test_ImageGetInfoJson_RedirectsToDeliverator()
        {
            // Arrange
            var expectedPath = new Uri("http://deliverator/iiif-img/2/1/image/info.json");
            
            // Act
            var response = await httpClient.GetAsync("/iiif-img/2/1/image/info.json");
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();

            // Assert
            proxyResponse.Uri.Should().Be(expectedPath);
            proxyResponse.Method.Should().Be(HttpMethod.Get);
        }
    }
}