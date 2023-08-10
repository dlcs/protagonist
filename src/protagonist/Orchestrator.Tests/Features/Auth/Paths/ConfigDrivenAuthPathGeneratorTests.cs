using System.Collections.Generic;
using DLCS.Core.Types;
using DLCS.Web.Response;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Orchestrator.Features.Auth.Paths;
using Orchestrator.Settings;

namespace Orchestrator.Tests.Features.Auth.Paths;

public class ConfigDrivenAuthPathGeneratorTests
{
    [Fact]
    public void GetAuthPathForRequest_Default()
    {
        // Arrange
        var sut = GetSut("default.com");
        var expected = "https://default.com/auth/99/test-auth";
        
        // Act
        var actual = sut.GetAuthPathForRequest("99", "test-auth");
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void GetAuthPathForRequest_Override()
    {
        // Arrange
        var sut = GetSut("test.example.com");
        var expected = "https://test.example.com/authentication_99/test-auth";
        
        // Act
        var actual = sut.GetAuthPathForRequest("99", "test-auth");
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void GetAuth2PathForRequest_Default()
    {
        // Arrange
        var sut = GetSut("default.com");
        var assetId = new AssetId(99, 100, "asset");
        var expected = "https://default.com/access_99/99/100/asset/test-auth";
        
        // Act
        var actual = sut.GetAuth2PathForRequest(assetId, "TestType", "test-auth");
        
        // Assert
        actual.Should().Be(expected);
    }
    
    [Fact]
    public void GetAuth2PathForRequest_Override()
    {
        // Arrange
        var sut = GetSut("test.example.com");
        var assetId = new AssetId(99, 100, "asset");
        var expected = "https://test.example.com/different_99/99/100/asset/test-auth";

        // Act
        var actual = sut.GetAuth2PathForRequest(assetId, "TestType", "test-auth");
        
        // Assert
        actual.Should().Be(expected);
    }

    private ConfigDrivenAuthPathGenerator GetSut(string host)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        var contextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => contextAccessor.HttpContext).Returns(context);
        request.Host = new HostString(host);
        request.Scheme = "https";

        var options = Options.Create(new OrchestratorSettings
        {
            Auth = new AuthSettings
            {
                AuthPathRules = new PathTemplateOptions
                {
                    Default = "/auth/{customer}/{behaviour}",
                    Overrides = new Dictionary<string, string>
                    {
                        ["test.example.com"] = "/authentication_{customer}/{behaviour}"
                    }
                },
                Auth2PathRules = new TypedPathTemplateOptions
                {
                    Defaults = new Dictionary<string, string>
                    {
                        ["TestType"] = "/access_{customer}/{assetId}/{accessService}"
                    },
                    Overrides = new Dictionary<string, Dictionary<string, string>>
                    {
                        ["test.example.com"] = new()
                        {
                            ["TestType"] = "/different_{customer}/{assetId}/{accessService}"
                        }
                    }
                }
            }
        });

        return new ConfigDrivenAuthPathGenerator(options, contextAccessor);
    }
}