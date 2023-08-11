using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using DLCS.Core.Types;
using IIIF;
using IIIF.Auth.V2;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging.Abstractions;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.Auth.V2;
using Test.Helpers.Http;

namespace Orchestrator.Tests.Infrastructure.Auth.V2;

public class IIIFAuth2ClientTests
{
    private readonly ControllableHttpMessageHandler httpHandler;
    private static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.Web);
    private readonly IIIFAuth2Client sut;

    public IIIFAuth2ClientTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        var httpClient = new HttpClient(httpHandler);
        httpClient.BaseAddress = new Uri("http://auth-2/");

        sut = new IIIFAuth2Client(httpClient, new NullLogger<IIIFAuth2Client>());
    }

    [Fact]
    public async Task GetAuthServicesForAsset_CallsCorrectPath_SingleRole()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = new List<string> { "role1" }
        };
        
        // Act
        await sut.GetAuthServicesForAsset(orchestrationImage.AssetId, orchestrationImage.Roles, CancellationToken.None);
        
        // Assert
        httpHandler.CallsMade.Should().ContainSingle(s => s == "http://auth-2/services/99/100/foo?roles=role1");
    }
    
    [Fact]
    public async Task GetAuthServicesForAsset_CallsCorrectPath_MultipleRoles()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = new List<string> { "role1", "role2", "role3" }
        };
        
        // Act
        await sut.GetAuthServicesForAsset(orchestrationImage.AssetId, orchestrationImage.Roles, CancellationToken.None);
        
        // Assert
        httpHandler.CallsMade.Should()
            .ContainSingle(s => s == "http://auth-2/services/99/100/foo?roles=role1,role2,role3");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetAuthServicesForAsset_ReturnsNull_IfHttpException(HttpStatusCode status)
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = new List<string> { "role1", "role2", "role3" }
        };

        httpHandler.SetResponse(new HttpResponseMessage(status));

        // Act
        var response = await sut.GetAuthServicesForAsset(orchestrationImage.AssetId, orchestrationImage.Roles,
            CancellationToken.None);

        // Assert
        response.Should().BeNull();
    }
    
    [Fact]
    public async Task GetAuthServicesForAsset_ReturnsNull_IfSuccess_ButUnableToDeserialise()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = new List<string> { "role1", "role2", "role3" }
        };

        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponseMessage.Content = new StringContent("{\"error\":\"uhoh\"}", Encoding.UTF8, "application/json");
        httpHandler.SetResponse(httpResponseMessage);

        // Act
        var response = await sut.GetAuthServicesForAsset(orchestrationImage.AssetId, orchestrationImage.Roles,
            CancellationToken.None);

        // Assert
        response.Should().BeNull();
    }
    
    [Fact]
    public async Task GetAuthServicesForAsset_ReturnsProbeService_IfSuccess()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = new List<string> { "role1", "role2", "role3" }
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
        var response = await sut.GetAuthServicesForAsset(orchestrationImage.AssetId, orchestrationImage.Roles,
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
            AssetId = AssetId.FromString("99/100/foo"), Roles = new List<string> { "role1" }
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
            AssetId = AssetId.FromString("99/100/foo"), Roles = new List<string> { "role1", "role2", "role3" }
        };
        
        // Act
        await sut.GetProbeServiceResult(orchestrationImage.AssetId, orchestrationImage.Roles, "accessToken",
            CancellationToken.None);
        
        // Assert
        httpHandler.CallsMade.Should()
            .ContainSingle(s => s == "http://auth-2/probe_internal/99/100/foo?roles=role1,role2,role3");
    }

    [Fact]
    public async Task GetAuthServicesForAsset_ReturnsErrorProbeService_IfHttpException()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = new List<string> { "role1", "role2", "role3" }
        };

        httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Act
        var response = await sut.GetProbeServiceResult(orchestrationImage.AssetId, orchestrationImage.Roles, "accessToken",
            CancellationToken.None);

        // Assert
        response.Status.Should().Be(500);
    }
    
    [Fact]
    public async Task GetAuthServicesForAsset_ReturnsDownstreamProbeService()
    {
        // Arrange
        var orchestrationImage = new OrchestrationImage
        {
            AssetId = AssetId.FromString("99/100/foo"), Roles = new List<string> { "role1", "role2", "role3" }
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
}