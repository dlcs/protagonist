using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Data;
using Test.Helpers.Integration;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Tests.Integration;

/// <summary>
/// Test of all requests handled by custom iiif-av handling
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class TimebasedHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;
    private readonly List<ImageDeliveryChannel> deliveryChannelsForTimebased = new()
    {
        new ImageDeliveryChannel()
        {
            Channel = AssetDeliveryChannels.Timebased,
            DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.AvDefaultAudio
        }
    };

    public TimebasedHandlingTests(ProtagonistAppFactory<Startup> factory, DlcsDatabaseFixture dbFixture)
    {
        this.dbFixture = dbFixture;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
            {
                services
                    .AddSingleton<IForwarderHttpClientFactory, TestProxyHttpClientFactory>()
                    .AddSingleton<IHttpForwarder, TestProxyForwarder>()
                    .AddSingleton<TestProxyHandler>();
            })
            .CreateClient(new WebApplicationFactoryClientOptions {AllowAutoRedirect = false});
        
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task Options_Returns200_WithCorsHeaders()
    {
        // Arrange
        var corsHeaders = new[]
        {
            "Access-Control-Allow-Origin", "Access-Control-Allow-Headers", "Access-Control-Allow-Methods"
        };
        const string path = "iiif-av/1/1/my-timebased/full/full/max/max/0/default.mp4";

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
        const string path = "iiif-av/1/1/my-timebased/full/full/max/max/0/default.mp4";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_UnknownSpace_Returns404()
    {
        // Arrange
        const string path = "iiif-av/99/5/my-timebased/full/full/max/max/0/default.mp4";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_UnknownImage_Returns404()
    {
        // Arrange
        const string path = "iiif-av/99/1/my-timebased/full/full/max/max/0/default.mp4";

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
        await dbFixture.DbContext.Images.AddTestAsset(id, notForDelivery: true, imageDeliveryChannels: deliveryChannelsForTimebased);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"iiif-av/{id}/full/full/max/max/0/default.mp4");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_Returns404_IfNoTimebasedChannel()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, imageDeliveryChannels:
        [
            new()
            {
                Channel = AssetDeliveryChannels.File,
                DeliveryChannelPolicyId = KnownDeliveryChannelPolicies.FileNone
            }
        ]);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"iiif-av/{id}/full/full/max/max/0/default.mp4");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_AssetDoesNotRequireAuth_ProxiesToS3Location()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "video/mpeg", origin: "/test/space",
            imageDeliveryChannels: deliveryChannelsForTimebased);
        await dbFixture.DbContext.SaveChangesAsync();
        var expectedPath =
            new Uri(
                $"https://protagonist-storage.s3.eu-west-1.amazonaws.com/{id}/full/full/max/max/0/default.mp4");
        
        // Act
        var response = await httpClient.GetAsync($"/iiif-av/{id}/full/full/max/max/0/default.mp4");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        proxyResponse.Uri.Should().Be(expectedPath);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_AssetRequiresAuth_Returns401_IfNoAuthProvided()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "video/mpeg",
            origin: "/test/space", roles: "basic", imageDeliveryChannels: deliveryChannelsForTimebased);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"/iiif-av/{id}/full/full/max/max/0/default.mp4");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_AssetRequiresAuth_Returns401_IfBearerTokenProvided_ButInvalid()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "video/mpeg",
            origin: "/test/space", roles: "basic", imageDeliveryChannels: deliveryChannelsForTimebased);
        await dbFixture.DbContext.SaveChangesAsync();
        const string bearerToken = "ababababab";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/iiif-av/{id}/full/full/max/max/0/default.mp4");
        request.Headers.Add("Authorization", $"bearer {bearerToken}");
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_AssetRequiresAuth_Returns401_IfBearerTokenValid()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "video/mpeg",
            origin: "/test/space", roles: "clickthrough", imageDeliveryChannels: deliveryChannelsForTimebased);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/iiif-av/{id}/full/full/max/max/0/default.mp4");
        request.Headers.Add("Authorization", $"bearer {authToken.Entity.BearerToken}");
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Head_AssetRequiresAuth_Returns200_IfBearerTokenValid()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "video/mpeg", 
            origin: "/test/space", roles: "clickthrough", imageDeliveryChannels: deliveryChannelsForTimebased);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Head, $"/iiif-av/{id}/full/full/max/max/0/default.mp4");
        request.Headers.Add("Authorization", $"bearer {authToken.Entity.BearerToken}");
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Head_AssetRequiresAuth_Returns401_IfBearerTokenProvided_ButInvalid()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "video/mpeg", 
            origin: "/test/space", roles: "clickthrough", imageDeliveryChannels: deliveryChannelsForTimebased);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(-15),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Head, $"/iiif-av/{id}/full/full/max/max/0/default.mp4");
        request.Headers.Add("Authorization", $"bearer {authToken.Entity.BearerToken}");
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_AssetRequiresAuth_Returns401_IfCookieProvided_ButInvalid()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "video/mpeg", 
            origin: "/test/space", roles: "basic", imageDeliveryChannels: deliveryChannelsForTimebased);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/iiif-av/{id}/full/full/max/max/0/default.mp4");
        request.Headers.Add("Cookie", "dlcs-token-99=blabla;");
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }

    [Fact]
    public async Task Get_AssetRequiresAuth_ProxiesToS3_IfCookieTokenValid()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "video/mpeg", 
            origin: "/test/space", roles: "clickthrough", imageDeliveryChannels: deliveryChannelsForTimebased);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();
        
        var expectedPath =
            new Uri(
                $"https://protagonist-storage.s3.eu-west-1.amazonaws.com/{id}/full/full/max/max/0/default.mp4");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/iiif-av/{id}/full/full/max/max/0/default.mp4");
        request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
        var response = await httpClient.SendAsync(request);
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();

        // Assert
        proxyResponse.Uri.Should().Be(expectedPath);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Head_AssetRequiresAuth_Returns200_IfCookieValid()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "video/mpeg", 
            origin: "/test/space", roles: "clickthrough", imageDeliveryChannels: deliveryChannelsForTimebased);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Head, $"/iiif-av/{id}/full/full/max/max/0/default.mp4");
        request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Head_AssetRequiresAuth_Returns401_IfCookieValid_ButUnobtainableRole()
    {
        // This is not a realistic test but confirms we will shortcut auth requests if 'unobtainable' role
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "video/mpeg", 
            origin: "/test/space", roles: Asset.UnobtainableRole, imageDeliveryChannels: deliveryChannelsForTimebased);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Head, $"/iiif-av/{id}/full/full/max/max/0/default.mp4");
        request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Head_AssetRequiresAuth_Returns401_IfCookieProvided_ButInvalid()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "video/mpeg", 
            origin: "/test/space", roles: "clickthrough", imageDeliveryChannels: deliveryChannelsForTimebased);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(-15),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Head, $"/iiif-av/{id}/full/full/max/max/0/default.mp4");
        request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
}
