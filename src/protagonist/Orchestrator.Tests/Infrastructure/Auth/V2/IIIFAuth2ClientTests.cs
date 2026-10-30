using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Linq;
using System.Threading;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using IIIF;
using IIIF.Auth.V2;
using IIIF.Serialisation;
using LazyCache;
using LazyCache.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Features.Auth;
using Orchestrator.Infrastructure.Auth.V2;
using Orchestrator.Settings;
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

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };

        sut = GetSut(new CacheSettings());
    }

    private IIIFAuth2Client GetSut(CacheSettings cacheSettings)
    {
        var httpClient = new HttpClient(httpHandler);
        httpClient.BaseAddress = new Uri("http://auth-2/");

        // Use a new cache per sut so results aren't shared between tests. SizeLimit set to match Orchestrator, which
        // requires every entry to specify a Size
        var appCache = new CachingService(
            new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 })));
        var authCookieManager = new AuthCookieManager(httpContextAccessor, Options.Create(new AuthSettings()));
        var orchestratorOptions = Options.Create(new OrchestratorSettings { Caching = cacheSettings });
        return new IIIFAuth2Client(httpClient, httpContextAccessor, appCache, authCookieManager, orchestratorOptions,
            new NullLogger<IIIFAuth2Client>());
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

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    public async Task VerifyAccess_CachesResult_IfGrantedOrDenied(HttpStatusCode statusCode, bool expected)
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage { AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"] };
        httpHandler.SetResponse(new HttpResponseMessage(statusCode));

        // Act
        var first = await sut.VerifyAccess(orchestrationImage, CancellationToken.None);
        var second = await sut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        first.Should().Be(expected);
        second.Should().Be(expected);
        httpHandler.CallsMade.Should().ContainSingle();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task VerifyAccess_DoesNotCacheResult_IfError(HttpStatusCode statusCode)
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage { AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"] };
        httpHandler.SetResponse(new HttpResponseMessage(statusCode));

        // Act
        var first = await sut.VerifyAccess(orchestrationImage, CancellationToken.None);
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));
        var second = await sut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        first.Should().BeFalse();
        second.Should().BeTrue("error result was not cached");
        httpHandler.CallsMade.Should().HaveCount(2);
    }

    [Fact]
    public async Task VerifyAccess_DoesNotShareResult_BetweenAuth2Cookies()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage { AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"] };
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        httpContextAccessor.HttpContext.Request.Headers.Cookie = "dlcs-auth2-99=user-one";
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);
        httpContextAccessor.HttpContext.Request.Headers.Cookie = "dlcs-auth2-99=user-two";
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);
        httpContextAccessor.HttpContext.Request.Headers.Cookie = "dlcs-auth2-99=user-one";
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should().HaveCount(2, "user-one result is cached, user-two is separate");
    }

    [Fact]
    public async Task VerifyAccess_SharesResult_IfOnlyNonAuth2CookieDiffers()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage { AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"] };
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        httpContextAccessor.HttpContext.Request.Headers.Cookie = "dlcs-auth2-99=user-one; dlcs-token-99=id=a";
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);
        httpContextAccessor.HttpContext.Request.Headers.Cookie = "dlcs-auth2-99=user-one; dlcs-token-99=id=b";
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should().ContainSingle("key uses Auth2 cookie only");
    }

    [Fact]
    public async Task VerifyAccess_DoesNotShareResult_BetweenClientIps_IfIpRole()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage { AssetId = AssetId.FromString("99/100/foo"), Roles = [IpRole] };
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);
        httpContextAccessor.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.6");
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should().HaveCount(2);
    }

    [Fact]
    public async Task VerifyAccess_SharesResult_BetweenClientIps_IfNoIpRole()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage { AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"] };
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);
        httpContextAccessor.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.6");
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should().ContainSingle("client IP is not forwarded so doesn't affect result");
    }

    [Fact]
    public async Task VerifyAccess_CallsPath_WithOrderedDistinctRoles_ExcludingUnobtainable()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = ["role3", Asset.UnobtainableRole, "role1", "role3"]
        };

        // Act
        await sut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should()
            .ContainSingle(s => s == "http://auth-2/verifyaccess/99/100/foo?roles=role1,role3");
    }

    [Fact]
    public async Task VerifyAccess_SharesResult_IfRolesInDifferentOrder()
    {
        // Arrange
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await sut.VerifyAccess(
            new OrchestrationImage { AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1", "role2"] },
            CancellationToken.None);
        await sut.VerifyAccess(
            new OrchestrationImage
            {
                AssetId = AssetId.FromString("99/100/foo"), Roles = ["role2", Asset.UnobtainableRole, "role1"]
            },
            CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should().ContainSingle("both resolve to the same roles string");
    }

    [Fact]
    public async Task VerifyAccess_DoesNotShareResult_BetweenDeliverables()
    {
        // Arrange
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await sut.VerifyAccess(new OrchestrationImage { AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"] },
            CancellationToken.None);
        await sut.VerifyAccess(new OrchestrationImage { AssetId = AssetId.FromString("99/100/bar"), Roles = ["role1"] },
            CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should().HaveCount(2);
    }

    [Fact]
    public async Task VerifyAccess_DoesNotCache_IfDisabled()
    {
        // Arrange
        var noCacheSut = GetSut(GetCacheSettings(granted: 0, denied: 0));
        var orchestrationImage = new OrchestrationImage { AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"] };
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await noCacheSut.VerifyAccess(orchestrationImage, CancellationToken.None);
        await noCacheSut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task VerifyAccess_UsesShortDefaultTtl_NotCacheDuration_IfNoOverride(HttpStatusCode statusCode)
    {
        // Arrange - preconfigured durations are long, defaults should be used rather than these
        var longDurationSut = GetSut(new CacheSettings
        {
            TimeToLive = new Dictionary<CacheSource, CacheGroupSettings>
            {
                [CacheSource.Memory] = new() { ShortTtlSecs = 600, DefaultTtlSecs = 600, LongTtlSecs = 600 }
            }
        });
        var orchestrationImage = new OrchestrationImage { AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"] };
        httpHandler.SetResponse(new HttpResponseMessage(statusCode));

        // Act
        await longDurationSut.VerifyAccess(orchestrationImage, CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(5.5));
        await longDurationSut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should().HaveCount(2, "default ttl (5s granted, 2s denied) has expired");
    }

    [Fact]
    public async Task VerifyAccess_UsesOverrideTtl_IfConfigured()
    {
        // Arrange
        var overrideSut = GetSut(GetCacheSettings(granted: 1, denied: 1));
        var orchestrationImage = new OrchestrationImage { AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"] };
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        await overrideSut.VerifyAccess(orchestrationImage, CancellationToken.None);
        await overrideSut.VerifyAccess(orchestrationImage, CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        await overrideSut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should().HaveCount(2, "cached for 1s then expired");
    }

    [Fact]
    public async Task VerifyAccess_CachesDenied_IfOnlyGrantedDisabled()
    {
        // Arrange
        var grantedDisabledSut = GetSut(GetCacheSettings(granted: 0, denied: 10));
        var orchestrationImage = new OrchestrationImage { AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"] };

        // Act
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.Forbidden));
        await grantedDisabledSut.VerifyAccess(orchestrationImage, CancellationToken.None);
        await grantedDisabledSut.VerifyAccess(orchestrationImage, CancellationToken.None);

        // Assert
        httpHandler.CallsMade.Should().ContainSingle("denied result is cached");
    }

    [Fact]
    public async Task VerifyAccess_ConcurrentRequests_ResultInSingleCall()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage { AssetId = AssetId.FromString("99/100/foo"), Roles = ["role1"] };
        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK));
        httpHandler.RegisterCallback(_ => Thread.Sleep(200));

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => sut.VerifyAccess(orchestrationImage, CancellationToken.None))));

        // Assert
        results.Should().AllBeEquivalentTo(true);
        httpHandler.CallsMade.Should().ContainSingle();
    }

    private static CacheSettings GetCacheSettings(int granted, int denied) => new()
    {
        TimeToLive = new Dictionary<CacheSource, CacheGroupSettings>
        {
            [CacheSource.Memory] = new()
            {
                Overrides = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    [CacheOverrideKeys.VerifyAccess] = granted,
                    [CacheOverrideKeys.VerifyAccessDenied] = denied,
                }
            }
        }
    };
}
