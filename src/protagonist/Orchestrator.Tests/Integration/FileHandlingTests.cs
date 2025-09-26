using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Data;
using Test.Helpers.Integration;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Tests.Integration;

/// <summary>
/// Tests of all /file/ requests
/// </summary>
[Trait("Category", "Integration")]
[Collection(OrchestratorCollection.CollectionName)]
public class FileHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;
    private readonly string stubAddress;
    private readonly List<ImageDeliveryChannel> deliveryChannelsForFile =
    [
        new()
        {
            Channel = AssetDeliveryChannels.File,
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.ImageDefault
        }
    ];

    private const string ValidAuth = "Basic dW5hbWU6cHdvcmQ=";

    public FileHandlingTests(ProtagonistAppFactory<Startup> factory, OrchestratorFixture orchestratorFixture)
    {
        dbFixture = orchestratorFixture.DbFixture;
        stubAddress = orchestratorFixture.ApiStub.Address;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithLocalStack(orchestratorFixture.LocalStackFixture)
            .WithTestServices(services =>
            {
                services
                    .AddSingleton<IForwarderHttpClientFactory, TestProxyHttpClientFactory>()
                    .AddSingleton<IHttpForwarder, TestProxyForwarder>()
                    .AddSingleton<TestProxyHandler>();
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        dbFixture.CleanUp();
        ConfigureStubbery(orchestratorFixture);
    }

    [Fact]
    public async Task Options_Returns200_WithCorsHeaders()
    {
        // Arrange
        var corsHeaders = new[]
        {
            "Access-Control-Allow-Origin", "Access-Control-Allow-Headers", "Access-Control-Allow-Methods"
        };
        const string path = "file/1/1/my-file.pdf";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKeys(corsHeaders);
    }        

    [Fact]
    public async Task Get_UnknownCustomer_Returns404()
    {
        // Arrange
        const string path = "file/1/1/my-file.pdf";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_UnknownSpace_Returns404()
    {
        // Arrange
        const string path = "file/99/5/my-file.pdf";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_UnknownImage_Returns404()
    {
        // Arrange
        const string path = "file/99/1/my-file.pdf";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Returns404_IfNotForDelivery()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, notForDelivery: true, imageDeliveryChannels: deliveryChannelsForFile);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Theory]
    [InlineData("iiif-img", 1)]
    [InlineData("iiif-av", 6)]
    public async Task Get_Returns404_IfNotFileDeliveryChannel(string deliveryChannel, int deliveryChannelPolicyId)
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, imageDeliveryChannels: new List<ImageDeliveryChannel>
        {
            new()
            {
                Channel = deliveryChannel,
                DeliveryChannelPolicyId = deliveryChannelPolicyId
            }
        });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_NotOptimisedOrigin_ReturnsFileFromDLCSStorage()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "text/plain",
            origin: $"{stubAddress}/testfile", imageDeliveryChannels: deliveryChannelsForFile);
        await dbFixture.DbContext.SaveChangesAsync();

        var expectedPath = new Uri($"https://protagonist-storage.s3.eu-west-1.amazonaws.com/{id}/original");

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        proxyResponse.Uri.Should().Be(expectedPath);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_OptimisedOrigin_ReturnsFile()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        var s3Key = $"{id}/this-is-where";
        await dbFixture.DbContext.Images.AddTestAsset(id, 
            mediaType: "text/plain",
            origin: $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{s3Key}", 
            imageDeliveryChannels: deliveryChannelsForFile);
        await dbFixture.DbContext.SaveChangesAsync();

        var expectedPathRegex = GetExpectedPathRegex(s3Key);

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.ToString().Should().MatchRegex(expectedPathRegex);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }

    [Fact]
    public async Task Get_RequiresAuth_Returns401_IfNoCookie()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "basic", imageDeliveryChannels: deliveryChannelsForFile);
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_RequiresAuth_Returns401_IfInvalidNoCookie()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "basic", imageDeliveryChannels: deliveryChannelsForFile);
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"file/{id}");
        request.Headers.Add("Cookie", "dlcs-token-99=blabla;");
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_RequiresAuth_Returns401_IfExpiredCookie()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "basic", imageDeliveryChannels: deliveryChannelsForFile);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(-1),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"file/{id}");
        request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_RequiresAuth_RedirectsToFile_IfCookieProvided()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        var s3Key = $"{id}/this-is-where";
        await dbFixture.DbContext.Images.AddTestAsset(id, 
            roles: "clickthrough",
            origin: $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{s3Key}", 
            imageDeliveryChannels: deliveryChannelsForFile);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();
        
        var expectedPathRegex = GetExpectedPathRegex(s3Key);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"file/{id}");
        request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
        var response = await httpClient.SendAsync(request);
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Set-Cookie");
        proxyResponse.Uri.ToString().Should().MatchRegex(expectedPathRegex);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }

    // Regex for presignedURL, port will depend on what localStack is using. Expires + Signature will always differ
    private static string GetExpectedPathRegex(string s3Key) =>
        $"https://localhost:\\d+/{LocalStackFixture.OriginBucketName}/{s3Key}\\?AWSAccessKeyId=foo\\&Expires=\\d+\\&Signature=.*";

    private static void ConfigureStubbery(OrchestratorFixture orchestratorFixture)
    {
        orchestratorFixture.ApiStub.Get("/testfile", (_, _) => "from-stub")
            .Header("Content-Type", "text/plain");

        orchestratorFixture.ApiStub.Get("/authfile", (_, _) => "secure-from-stub")
            .Header("Content-Type", "text/plain")
            .IfHeader("Authorization", ValidAuth);

        orchestratorFixture.ApiStub.Get("/forbiddenfile", (_, _) => new ForbidResult());
    }
}
