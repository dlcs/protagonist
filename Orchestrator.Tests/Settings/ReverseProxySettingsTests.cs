using System;
using System.Collections.Generic;
using FluentAssertions;
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;
using Xunit;

namespace Orchestrator.Tests.Settings
{
    public class ReverseProxySettingsTests
    {
        private readonly ReverseProxySettings sut;
        
        public ReverseProxySettingsTests()
        {
            sut = new ReverseProxySettings
            {
                Clusters = new Dictionary<string, ClusterConfig>
                {
                    ["deliverator"] = new()
                    {
                        Destinations = new Dictionary<string, DestinationConfig>
                        {
                            ["one"] = new() {Address = new Uri("https://orchestrator")}
                        }
                    },
                    ["thumbs"] = new()
                    {
                        Destinations = new Dictionary<string, DestinationConfig>
                        {
                            ["one"] = new() {Address = new Uri("https://thumbs")}
                        }
                    },
                    ["image_server"] = new()
                    {
                        Destinations = new Dictionary<string, DestinationConfig>
                        {
                            ["one"] = new() {Address = new Uri("https://image_server")}
                        }
                    },
                    ["varnish_cache"] = new()
                    {
                        Destinations = new Dictionary<string, DestinationConfig>
                        {
                            ["one"] = new() {Address = new Uri("https://varnish_cache")},
                            ["two"] = new() {Address = new Uri("https://other_varnish_cache")}
                        }
                    }
                }
            };
        }
        
        [Theory]
        [InlineData(ProxyDestination.Orchestrator, "https://orchestrator")]
        [InlineData(ProxyDestination.Thumbs, "https://thumbs")]
        [InlineData(ProxyDestination.ImageServer, "https://image_server")]
        public void GetAddressForCluster_ReturnsFirstEntry_ForKnownClusters(ProxyDestination destination,
            string expected)
        {
            // Arrange
            var expectedUri = new Uri(expected);
            
            // Act
            var actual = sut.GetAddressForProxyTarget(destination);
            
            // Assert
            actual.Should().Be(expectedUri);
        }
        
        [Theory]
        [InlineData(ProxyDestination.S3)]
        [InlineData(ProxyDestination.Unknown)]
        public void GetAddressForCluster_ReturnsNull_ForUnknownAndS3(ProxyDestination destination)
        {
            // Act
            var actual = sut.GetAddressForProxyTarget(destination);
            
            // Assert
            actual.Should().BeNull();
        }
    }
}