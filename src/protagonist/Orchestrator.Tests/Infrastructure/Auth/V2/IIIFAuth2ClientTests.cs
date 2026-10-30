using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using IIIF;
using IIIF.Auth.V2;
using IIIF.Serialisation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.Auth.V2;
using Test.Helpers.Http;

namespace Orchestrator.Tests.Infrastructure.Auth.V2;

public class IIIFAuth2ClientTests
{
    private const string IpRole = "https://api.dlcs.io/customers/99/roles/ip:reading-room";
    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly HttpContextAccessor httpContextAccessor;
    private readonly IIIFAuth2Client sut;

    public IIIFAuth2ClientTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        var httpClient = new HttpClient(httpHandler);
        httpClient.BaseAddress = new Uri("http://auth-2/");

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };

        sut = new IIIFAuth2Client(httpClient, httpContextAccessor, new NullLogger<IIIFAuth2Client>());
    }

    [Fact]
    public async Task GetAuthServices_NoOp_IfUnobtainableRoleOnly()
    {
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = [Asset.UnobtainableRole]
        };
        
        // Act
        var result = await sut.GetAuthServices(orchestrationImage.AssetId, orchestrationImage.Roles,
            CancellationToken.None);
        
        // Assert
        httpHandler.CallsMade.Should().HaveCount(0);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAuthServices_CallsCorrectPath_SingleRole()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"]
        };
        
        // Act
        await sut.GetAuthServices(orchestrationImage.AssetId, orchestrationImage.Roles, CancellationToken.None);
        
        // Assert
        httpHandler.CallsMade.Should().ContainSingle(s => s == "http://auth-2/services/99/100/foo?roles=role1");
    }

    [Fact]
    public async Task GetAuthServices_CallsCorrectPath_IgnoringUnobtainableRole()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1", Asset.UnobtainableRole]
        };

        // Act
        await sut.GetAuthServices(orchestrationImage.AssetId, orchestrationImage.Roles, CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should().ContainSingle(s => s == "http://auth-2/services/99/100/foo?roles=role1");
    }

    [Fact]
    public async Task GetAuthServices_CallsCorrectPath_MultipleRoles()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1", "role2", "role3"]
        };
        
        // Act
        await sut.GetAuthServices(orchestrationImage.AssetId, orchestrationImage.Roles, CancellationToken.None);
        
        // Assert
        httpHandler.CallsMade.Should()
            .ContainSingle(s => s == "http://auth-2/services/99/100/foo?roles=role1,role2,role3");
    }

    [Fact]
    public async Task GetAuthServices_Adjunct_CallsCorrectPath()
    {
        // Act
        await sut.GetAuthServices(new DeliverableId(AssetId.FromString("99/100/foo"), "mets.xml"),
            ["role1"], CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should()
            .ContainSingle(s => s == "http://auth-2/services/99/100/foo/mets.xml?roles=role1");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetAuthServices_ReturnsNull_IfHttpException(HttpStatusCode status)
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1", "role2", "role3"]
        };

        httpHandler.SetResponse(new HttpResponseMessage(status));

        // Act
        var response = await sut.GetAuthServices(orchestrationImage.AssetId, orchestrationImage.Roles,
            CancellationToken.None);

        // Assert
        response.Should().BeNull();
    }
    
    [Fact]
    public async Task GetAuthServices_ReturnsNull_IfSuccess_ButUnableToDeserialise()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1", "role2", "role3"]
        };

        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponseMessage.Content = new StringContent("{\"error\":\"uhoh\"}", Encoding.UTF8, "application/json");
        httpHandler.SetResponse(httpResponseMessage);

        // Act
        var response = await sut.GetAuthServices(orchestrationImage.AssetId, orchestrationImage.Roles,
            CancellationToken.None);

        // Assert
        response.Should().BeNull();
    }
    
    [Fact]
    public async Task GetAuthServices_ReturnsProbeService_IfSuccess()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1", "role2", "role3"]
        };

        var probeService = new AuthProbeService2
        {
            Id = "http://probeservice",
            Service = new List<IService> { new AuthAccessService2 { Profile = "external" } }
        };
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponseMessage.Content = new StringContent(probeService.AsJson(), Encoding.UTF8, "application/json");
        httpHandler.SetResponse(httpResponseMessage);

        // Act
        var response = await sut.GetAuthServices(orchestrationImage.AssetId, orchestrationImage.Roles,
            CancellationToken.None);

        // Assert
        response.Should().BeEquivalentTo(probeService);
    }

    [Fact]
    public async Task GetProbeServiceResult_CallsCorrectPath_SingleRole()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"]
        };
        
        // Act
        await sut.GetProbeServiceResult(orchestrationImage.AssetId, orchestrationImage.Roles, "accessToken",
            CancellationToken.None);
        
        // Assert
        httpHandler.CallsMade.Should().ContainSingle(s => s == "http://auth-2/probe_internal/99/100/foo?roles=role1");
    }
    
    [Fact]
    public async Task GetProbeServiceResult_CallsCorrectPath_MultipleRole()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1", "role2", "role3"]
        };
        
        // Act
        await sut.GetProbeServiceResult(orchestrationImage.AssetId, orchestrationImage.Roles, "accessToken",
            CancellationToken.None);
        
        // Assert
        httpHandler.CallsMade.Should()
            .ContainSingle(s => s == "http://auth-2/probe_internal/99/100/foo?roles=role1,role2,role3");
    }

    [Fact]
    public async Task GetProbeServiceResult_Adjunct_CallsCorrectPath()
    {
        // Act
        await sut.GetProbeServiceResult(new DeliverableId(AssetId.FromString("99/100/foo"), "mets.xml"),
            ["role1", "role2"], "accessToken", CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should()
            .ContainSingle(s => s == "http://auth-2/probe_internal/99/100/foo/mets.xml?roles=role1,role2");
    }

    [Fact]
    public async Task GetProbeServiceResult_SendsAuthorization_IfAccessToken()
    {
        // Arrange
        HttpRequestMessage request = null;
        httpHandler.RegisterCallback(r => request = r);

        // Act
        await sut.GetProbeServiceResult(AssetId.FromString("99/100/foo"), ["role1"], "accessToken",
            CancellationToken.None);

        // Assert
        request.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "accessToken"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetProbeServiceResult_DoesNotSendAuthorization_IfNoAccessToken(string accessToken)
    {
        // Arrange
        HttpRequestMessage request = null;
        httpHandler.RegisterCallback(r => request = r);

        // Act
        await sut.GetProbeServiceResult(AssetId.FromString("99/100/foo"), [IpRole], accessToken,
            CancellationToken.None);

        // Assert
        request.Headers.Authorization.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetProbeServiceResult_ReturnsMissingCredentials_WithoutCall_IfNoAccessToken_AndNoIpRole(
        string accessToken)
    {
        // Act
        var result = await sut.GetProbeServiceResult(AssetId.FromString("99/100/foo"), ["role1"], accessToken,
            CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should().BeEmpty();
        result.Should().BeEquivalentTo(AuthProbeResult2Builder.MissingCredentials);
    }

    [Fact]
    public async Task GetProbeServiceResult_SendsForwardedFor_IfIpRole()
    {
        // Arrange
        HttpRequestMessage request = null;
        httpHandler.RegisterCallback(r => request = r);

        // Act
        await sut.GetProbeServiceResult(AssetId.FromString("99/100/foo"), ["role1", IpRole], null,
            CancellationToken.None);

        // Assert
        request.Headers.GetValues("X-Forwarded-For").Should().ContainSingle(v => v == "10.0.0.5");
    }

    [Fact]
    public async Task GetProbeServiceResult_DoesNotSendForwardedFor_IfNoIpRole()
    {
        // Arrange
        HttpRequestMessage request = null;
        httpHandler.RegisterCallback(r => request = r);

        // Act
        await sut.GetProbeServiceResult(AssetId.FromString("99/100/foo"), ["role1"], "accessToken",
            CancellationToken.None);

        // Assert
        request.Headers.Contains("X-Forwarded-For").Should().BeFalse();
    }

    [Fact]
    public async Task GetProbeServiceResult_DoesNotSendForwardedFor_IfIpRole_ButNoClientIp()
    {
        // Arrange
        HttpRequestMessage request = null;
        httpHandler.RegisterCallback(r => request = r);
        httpContextAccessor.HttpContext.Connection.RemoteIpAddress = null;

        // Act
        await sut.GetProbeServiceResult(AssetId.FromString("99/100/foo"), [IpRole], null, CancellationToken.None);

        // Assert
        request.Headers.Contains("X-Forwarded-For").Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthServices_ReturnsErrorProbeService_IfHttpException()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1", "role2", "role3"]
        };

        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Act
        var response = await sut.GetProbeServiceResult(orchestrationImage.AssetId, orchestrationImage.Roles, "accessToken",
            CancellationToken.None);

        // Assert
        response.Status.Should().Be(500);
    }
    
    [Fact]
    public async Task GetAuthServices_ReturnsDownstreamProbeService()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1", "role2", "role3"]
        };

        var probeServiceResult = new AuthProbeResult2 { Status = 200 };
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponseMessage.Content = new StringContent(probeServiceResult.AsJson(), Encoding.UTF8, "application/json");
        httpHandler.SetResponse(httpResponseMessage);

        // Act
        var response = await sut.GetProbeServiceResult(orchestrationImage.AssetId, orchestrationImage.Roles, "accessToken",
            CancellationToken.None);

        // Assert
        response.Should().BeEquivalentTo(probeServiceResult);
    }
    
    [Fact]
    public async Task VerifyAccess_CallsCorrectPath_SingleRole()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"]
        };
        
        // Act
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);
        
        // Assert
        httpHandler.CallsMade.Should().ContainSingle(s => s == "http://auth-2/verifyaccess/99/100/foo?roles=role1");
    }
    
    [Fact]
    public async Task VerifyAccess_CallsCorrectPath_MultipleRole()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1", "role2", "role3"]
        };
        
        // Act
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);
        
        // Assert
        httpHandler.CallsMade.Should()
            .ContainSingle(s => s == "http://auth-2/verifyaccess/99/100/foo?roles=role1,role2,role3");
    }

    [Fact]
    public async Task VerifyAccess_Adjunct_CallsCorrectPath_SingleRole()
    {
        // Act
        var orchestrationAdjunct = new OrchestrationAdjunct
        {
            AssetId = AssetId.FromString("99/100/foo"), 
            Roles = ["role1"],
            Id = "mets.xml"
        };
        await sut.VerifyAccess(orchestrationAdjunct, CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should()
            .ContainSingle(s => s == "http://auth-2/verifyaccess/99/100/foo/mets.xml?roles=role1");
    }

    [Fact]
    public async Task VerifyAccess_Adjunct_CallsCorrectPath_MultipleRole()
    {
        // Act
        var orchestrationAdjunct = new OrchestrationAdjunct
        {
            AssetId = AssetId.FromString("99/100/foo"), 
            Roles = ["role1", "role2", "role3"],
            Id = "mets.xml"
        };
        await sut.VerifyAccess(orchestrationAdjunct, CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should()
            .ContainSingle(s => s == "http://auth-2/verifyaccess/99/100/foo/mets.xml?roles=role1,role2,role3");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task VerifyAccess_Adjunct_ReturnsFalse_IfHttpException(HttpStatusCode statusCode)
    {
        // Arrange
        var orchestrationAdjunct = new OrchestrationAdjunct
        {
            AssetId = AssetId.FromString("99/100/foo"), 
            Roles = ["role1"],
            Id = "mets.xml"
        };
        httpHandler.SetResponse(new HttpResponseMessage(statusCode));

        // Act
        var response = await sut.VerifyAccess(orchestrationAdjunct, CancellationToken.None);

        // Assert
        response.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAccess_Adjunct_ReturnsTrue_IfSuccess()
    {
        // Arrange
        var orchestrationAdjunct = new OrchestrationAdjunct
        {
            AssetId = AssetId.FromString("99/100/foo"), 
            Roles = ["role1"],
            Id = "mets.xml"
        };
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var response = await sut.VerifyAccess(orchestrationAdjunct, CancellationToken.None);

        // Assert
        response.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAccess_SendsForwardedFor_IfIpRole()
    {
        // Arrange
        HttpRequestMessage request = null;
        httpHandler.RegisterCallback(r => request = r);
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1", IpRole]
        };

        // Act
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        request.Headers.GetValues("X-Forwarded-For").Should().ContainSingle(v => v == "10.0.0.5");
    }

    [Fact]
    public async Task VerifyAccess_DoesNotSendForwardedFor_IfNoIpRole()
    {
        // Arrange
        HttpRequestMessage request = null;
        httpHandler.RegisterCallback(r => request = r);
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"]
        };

        // Act
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        request.Headers.Contains("X-Forwarded-For").Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task VerifyAccess_ReturnsFalse_IfHttpException(HttpStatusCode statusCode)
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1", "role2", "role3"]
        };

        httpHandler.SetResponse(new HttpResponseMessage(statusCode));

        // Act
        var response = await sut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        response.Should().Be(false);
    }
    
    [Fact]
    public async Task GetAuthServices_ReturnsTrue_IfSuccess()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1", "role2", "role3"]
        };

        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var response =
            await sut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        response.Should().BeTrue();
    }
}
