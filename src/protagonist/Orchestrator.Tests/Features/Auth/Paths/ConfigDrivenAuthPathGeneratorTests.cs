using System.Collections.Generic;
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
                }
            }
        });

        return new ConfigDrivenAuthPathGenerator(options, contextAccessor);
    }
}