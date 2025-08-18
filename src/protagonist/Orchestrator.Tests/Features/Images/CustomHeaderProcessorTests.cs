using System.Collections.Generic;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets.CustomHeaders;
using Orchestrator.Assets;
using Orchestrator.Features.Images;
using Orchestrator.Infrastructure.ReverseProxy;

namespace Orchestrator.Tests.Features.Images;

public class CustomHeaderProcessorTests
{
    private const int Space = 10;
    private const string Header = "x-test-header";
    private const string Role = "sample-role";
    
    [Fact]
    public void SetProxyImageServerHeaders_DoesNotChangeHeaders_IfCustomHeadersNull()
    {
        // Arrange
        List<CustomHeader> customerCustomHeaders = null;
        var orchestrationImage = new OrchestrationImage();
        var proxyImageServerResult =
            new ProxyImageServerResult(orchestrationImage, false);
        
        // Act
        CustomHeaderProcessor.SetProxyImageHeaders(customerCustomHeaders, orchestrationImage,
            proxyImageServerResult);
        
        // Assert
        proxyImageServerResult.Headers.Should().BeEmpty();
    }
    
    [Fact]
    public void SetProxyImageServerHeaders_DoesNotChangeHeaders_IfCustomHeadersEmpty()
    {
        // Arrange
        List<CustomHeader> customerCustomHeaders = new();
        var orchestrationImage = new OrchestrationImage();
        var proxyImageServerResult =
            new ProxyImageServerResult(orchestrationImage, false);
        
        // Act
        CustomHeaderProcessor.SetProxyImageHeaders(customerCustomHeaders, orchestrationImage,
            proxyImageServerResult);
        
        // Assert
        proxyImageServerResult.Headers.Should().BeEmpty();
    }
    
    [Fact]
    public void SetProxyImageServerHeaders_UsesNoMatchingSpaceOrRole_Restricted()
    {
        // Arrange
        var customerCustomHeaders = new List<CustomHeader>
        {
            new() { Space = null, Role = null, Key = Header, Value = "no space or role" },
        };

        var orchestrationImage = GetOrchestrationImage(true);
        var proxyImageServerResult =
            new ProxyImageServerResult(orchestrationImage, false);
        
        // Act
        CustomHeaderProcessor.SetProxyImageHeaders(customerCustomHeaders, orchestrationImage,
            proxyImageServerResult);
        
        // Assert
        proxyImageServerResult.Headers[Header].ToString().Should().Be("no space or role");
    }
    
    [Fact]
    public void SetProxyImageServerHeaders_PrefersMatchingSpaceRole_Restricted()
    {
        // Arrange
        var customerCustomHeaders = new List<CustomHeader>
        {
            new() { Space = null, Role = null, Key = Header, Value = "no space or role" },
            new() { Space = Space, Role = "", Key = Header, Value = "space" },
        };

        var orchestrationImage = GetOrchestrationImage(true);
        var proxyImageServerResult =
            new ProxyImageServerResult(orchestrationImage, false);
        
        // Act
        CustomHeaderProcessor.SetProxyImageHeaders(customerCustomHeaders, orchestrationImage,
            proxyImageServerResult);
        
        // Assert
        proxyImageServerResult.Headers[Header].ToString().Should().Be("space");
    }
    
    [Fact]
    public void SetProxyImageServerHeaders_PrefersMatchingSpace_Restricted()
    {
        // Arrange
        var customerCustomHeaders = new List<CustomHeader>
        {
            new() { Space = null, Role = Role, Key = Header, Value = "role" },
            new() { Space = null, Role = null, Key = Header, Value = "no space or role" },
            new() { Space = Space, Role = "", Key = Header, Value = "space" },
        };

        var orchestrationImage = GetOrchestrationImage(true);
        var proxyImageServerResult =
            new ProxyImageServerResult(orchestrationImage, false);
        
        // Act
        CustomHeaderProcessor.SetProxyImageHeaders(customerCustomHeaders, orchestrationImage,
            proxyImageServerResult);
        
        // Assert
        proxyImageServerResult.Headers[Header].ToString().Should().Be("role");
    }
    
    [Fact]
    public void SetProxyImageServerHeaders_PrefersMatchingSpaceAndRole_Restricted()
    {
        // Arrange
        var customerCustomHeaders = new List<CustomHeader>
        {
            new() { Space = null, Role = Role, Key = Header, Value = "role" },
            new() { Space = Space, Role = Role, Key = Header, Value = "space and role" },
            new() { Space = -1, Role = null, Key = Header, Value = "no space or role" },
            new() { Space = Space, Role = "", Key = Header, Value = "space" },
        };

        var orchestrationImage = GetOrchestrationImage(true);
        var proxyImageServerResult =
            new ProxyImageServerResult(orchestrationImage, false);
        
        // Act
        CustomHeaderProcessor.SetProxyImageHeaders(customerCustomHeaders, orchestrationImage,
            proxyImageServerResult);
        
        // Assert
        proxyImageServerResult.Headers[Header].ToString().Should().Be("space and role");
    }
    
    [Fact]
    public void SetProxyImageServerHeaders_UsesNoMatchingSpaceOrRole_Open()
    {
        // Arrange
        var customerCustomHeaders = new List<CustomHeader>
        {
            new() { Space = null, Role = null, Key = Header, Value = "no space or role" },
            new() { Space = null, Role = Role, Key = Header, Value = "role" },
            new() { Space = Space, Role = Role, Key = Header, Value = "space and role" },
        };

        var orchestrationImage = GetOrchestrationImage(false);
        var proxyImageServerResult =
            new ProxyImageServerResult(orchestrationImage, false);
        
        // Act
        CustomHeaderProcessor.SetProxyImageHeaders(customerCustomHeaders, orchestrationImage,
            proxyImageServerResult);
        
        // Assert
        proxyImageServerResult.Headers[Header].ToString().Should().Be("no space or role");
    }
    
    [Fact]
    public void SetProxyImageServerHeaders_PrefersMatchingSpace_Open()
    {
        // Arrange
        var customerCustomHeaders = new List<CustomHeader>
        {
            new() { Space = null, Role = Role, Key = Header, Value = "role" },
            new() { Space = Space, Role = Role, Key = Header, Value = "space and role" },
            new() { Space = null, Role = null, Key = Header, Value = "no space or role" },
            new() { Space = Space, Role = "", Key = Header, Value = "space" },
        };

        var orchestrationImage = GetOrchestrationImage(false);
        var proxyImageServerResult =
            new ProxyImageServerResult(orchestrationImage, false);
        
        // Act
        CustomHeaderProcessor.SetProxyImageHeaders(customerCustomHeaders, orchestrationImage,
            proxyImageServerResult);
        
        // Assert
        proxyImageServerResult.Headers[Header].ToString().Should().Be("space");
    }

    private OrchestrationImage GetOrchestrationImage(bool isRestricted)
    {
        var image = new OrchestrationImage
            { AssetId = new AssetId(99, Space, "whatever"), RequiresAuth = isRestricted };
        if (isRestricted) image.Roles = Role.AsList();

        return image;
    }
}