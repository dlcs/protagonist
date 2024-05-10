using DLCS.Core.Collections;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace Orchestrator.Tests.Infrastructure.ReverseProxy;

public class DownstreamDestinationSelectorTests
{
    private readonly DownstreamDestinationSelector sut;
    private readonly ILoadBalancingPolicy loadBalancingPolicy;
    private readonly IProxyStateLookup proxyStateLookup;
    
    public DownstreamDestinationSelectorTests()
    {
        loadBalancingPolicy = A.Fake<ILoadBalancingPolicy>();
        proxyStateLookup = A.Fake<IProxyStateLookup>();
        var optionsMonitor = A.Fake<IOptionsMonitor<OrchestratorSettings>>();
        A.CallTo(() => optionsMonitor.CurrentValue).Returns(new OrchestratorSettings());

        sut = new DownstreamDestinationSelector(new NullLogger<DownstreamDestinationSelector>(),
            loadBalancingPolicy.AsList(), proxyStateLookup, optionsMonitor);
    }
    
    [Fact]
    public void GetRandomDestinationAddress_Null_IfClusterNotFound()
    {
        var randomAddress = sut.GetRandomDestinationAddress(ProxyDestination.Orchestrator);

        randomAddress.Should().BeNull();
    }
    
    [Fact]
    public void GetRandomDestinationAddress_Null_IfClusterHasNoAvailableDestinations()
    {
        // Arrange
        const string clusterName = "cantaloupe";
        ClusterState clusterState = new ClusterState(clusterName);
        A.CallTo(() => proxyStateLookup.TryGetCluster(clusterName, out clusterState)).Returns(true);
        
        // Act
        var randomAddress = sut.GetRandomDestinationAddress(ProxyDestination.Orchestrator);

        // Assert
        randomAddress.Should().BeNull();
    }
    
    // NOTE - the Yarp models are difficult to construct so can't be fully tested
}