using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Tests.Integration.Infrastructure;
using Stubbery;
using Test.Helpers.Integration;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Tests.Integration
{
    public class FallbackProxyTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly HttpClient httpClient;

        public FallbackProxyTests(ProtagonistAppFactory<Startup> factory)
        {
            httpClient = factory
                .WithTestServices(services =>
                {
                    services.AddSingleton<IForwarderHttpClientFactory, TestProxyHttpClientFactory>();
                    services.AddSingleton<TestProxyHandler>();
                })
                .CreateClient();
        }
        
        [Theory]
        [InlineData("/auth/2/1/something")]
        [InlineData("/iiif-resource/2/1/something")]
        [InlineData("/info/2/1/something")]
        [InlineData("/pdf/2/1/something")]
        [InlineData("/pdf-resource/2/1/something")]
        [InlineData("/pdf-control/2/1/something")]
        [InlineData("/raw-resource/2/1/something")]
        public async Task Test_FallbackRoute(string path)
        {
            // NOTE: This is everything except iiif-img and iiif-av routes
            // Arrange
            var expectedPath = new Uri($"http://deliverator{path}");
            
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();

            proxyResponse.Uri.Should().Be(expectedPath);
            proxyResponse.Method.Should().Be(HttpMethod.Get);
        }

        [Fact]
        public async Task Favicon_Returns404()
        {
            // Act
            var response = await httpClient.GetAsync("favicon.ico");
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}