using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.Auth.Entities;
using IIIF.ImageApi.V3;
using IIIF.Serialisation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Orchestrator.Features.Images.ImageServer;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Data;
using Test.Helpers.Integration;
using Yarp.ReverseProxy.Forwarder;
// ReSharper disable PossibleNullReferenceException

namespace Orchestrator.Tests.Integration;

/// <summary>
/// Tests of info.json requests for restricted images when "InfoJsonCanBeNon200" is true. This restores the legacy
/// behaviour where info.json returns 401 if the user doesn't have access, and validates bearer tokens.
/// </summary>
/// <remarks>See <see cref="ImageHandlingTests"/> for the default behaviour, where info.json is always 200</remarks>
[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class ImageHandlingInfoJsonNon200Tests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;
    private readonly IAmazonS3 amazonS3;
    private readonly FakeImageOrchestrator orchestrator = new();
    private const string SizesJsonContent = "{\"o\":[[800,800],[400,400],[200,200]],\"a\":[]}";

    private readonly List<ImageDeliveryChannel> deliveryChannelsForImage;

    public ImageHandlingInfoJsonNon200Tests(ProtagonistAppFactory<Startup> factory, StorageFixture storageFixture)
    {
        dbFixture = storageFixture.DbFixture;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithConfigValue("OrchestrateOnInfoJson", "true")
            .WithConfigValue("InfoJsonCanBeNon200", "true")
            .WithLocalStack(storageFixture.LocalStackFixture)
            .WithTestServices(services =>
            {
                services
                    .AddSingleton<IForwarderHttpClientFactory, TestProxyHttpClientFactory>()
                    .AddSingleton<IHttpForwarder, TestProxyForwarder>()
                    .AddSingleton<IImageServerClient, FakeImageServerClient>()
                    .AddSingleton<IIIIFAuthBuilder, FakeAuth2Client>()
                    .AddSingleton<IImageOrchestrator>(orchestrator)
                    .AddSingleton<TestProxyHandler>();
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        deliveryChannelsForImage = dbFixture.DbContext.GetImageDeliveryChannels();

        dbFixture.CleanUp();
    }

    [Fact]
    public async Task GetInfoJson_RestrictedImage_Correct()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        const string roleName = "my-test-role";
        const string authServiceName = "my-auth-service";
        const string logoutServiceName = "my-logout-service";
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: roleName, openFullMax: 500,
            imageDeliveryChannels: deliveryChannelsForImage);
        await dbFixture.DbContext.Roles.AddAsync(new Role
        {
            Customer = 99, Id = roleName, Name = "test-role", AuthService = authServiceName
        });
        await dbFixture.DbContext.AuthServices.AddRangeAsync(new AuthService
        {
            Name = "test-service", Customer = 99, Id = authServiceName, Profile = "http://iiif.io/api/auth/1/login",
            Label = "the-label", Description = "the-description", ChildAuthService = logoutServiceName
        }, new AuthService
        {
            Name = "test-service-logout", Customer = 99, Id = logoutServiceName,
            Profile = "http://iiif.io/api/auth/1/logout",
        });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[400,400],[200,200]]}"
        });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/info.json");

        // Assert
        var responseStream = await response.Content.ReadAsStreamAsync();
        var infoJson = responseStream.FromJsonStream<ImageService3>();

        infoJson.Id.Should().Be($"http://localhost/iiif-img/{id}");
        infoJson.Service.Should().HaveCount(1, "Contains v2 auth services");
        infoJson.Service?.Single().Id.Should().Be($"http://localhost/auth/v2/probe/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeFalse();
        response.Headers.CacheControl.Private.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }
    
    [Fact]
    public async Task GetInfoJson_RestrictedImage_Correct_CustomPathRules()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        const string roleName = "my-test-role";
        const string authServiceName = "my-auth-service";
        const string logoutServiceName = "my-logout-service";

        await dbFixture.DbContext.Images.AddTestAsset(id, roles: roleName, openFullMax: 500,
            imageDeliveryChannels: deliveryChannelsForImage);
        await dbFixture.DbContext.Roles.AddAsync(new Role
        {
            Customer = 99, Id = roleName, Name = "test-role", AuthService = authServiceName
        });

        await dbFixture.DbContext.AuthServices.AddRangeAsync(new AuthService
        {
            Name = "test-service", Customer = 99, Id = authServiceName, Profile = "http://iiif.io/api/auth/1/login",
            Label = "the-label", Description = "the-description", ChildAuthService = logoutServiceName
        }, new AuthService
        {
            Name = "test-service-logout", Customer = 99, Id = logoutServiceName,
            Profile = "http://iiif.io/api/auth/1/logout",
        });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[400,400],[200,200]]}"
        });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/{id}/info.json");
        request.Headers.Add("Host", "my-proxy.com");
        var response = await httpClient.SendAsync(request);

        // Assert
        var responseStream = await response.Content.ReadAsStreamAsync();
        var infoJson = responseStream.FromJsonStream<ImageService3>();

        infoJson.Id.Should()
            .Be($"http://my-proxy.com/const_value/99/{nameof(GetInfoJson_RestrictedImage_Correct_CustomPathRules)}");
        infoJson.Service.Should().HaveCount(1, "Contains v2 auth services only");
        infoJson.Service?.Single().Id.Should().Be($"http://my-proxy.com/proxy-probe/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeFalse();
        response.Headers.CacheControl.Private.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }
    
    [Fact]
    public async Task GetInfoJson_RestrictedImage_UnobtainableRole_HasNoService()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, openFullMax: 500, roles: Asset.UnobtainableRole,
            imageDeliveryChannels: deliveryChannelsForImage);

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[400,400],[200,200]]}"
        });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/info.json");

        // Assert
        var responseStream = await response.Content.ReadAsStreamAsync();
        var infoJson = responseStream.FromJsonStream<ImageService3>();

        infoJson.Id.Should().Be($"http://localhost/iiif-img/{id}");
        infoJson.Service.Should().BeNull();
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeFalse();
        response.Headers.CacheControl.Private.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }
    
    [Fact]
    public async Task GetInfoJson_RestrictedImage_WithUnknownRole_Returns401WithoutServices()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "unknown-role", openFullMax: 500,
            imageDeliveryChannels: deliveryChannelsForImage);

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[400,400],[200,200]]}"
        });
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/info.json");

        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
        jsonResponse["services"].Should().BeNull();
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeFalse();
        response.Headers.CacheControl.Private.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }
    
    [Fact]
    public async Task GetInfoJson_RestrictedImage_WithUnknownRole_Returns401_IfNoBearerTokenProvided()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", openFullMax: 500,
            imageDeliveryChannels: deliveryChannelsForImage);
        await dbFixture.DbContext.SaveChangesAsync();
        
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        
        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/info.json");

        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeFalse();
        response.Headers.CacheControl.Private.Should().BeTrue();
    }
    
    [Fact]
    public async Task GetInfoJson_RestrictedImage_WithUnknownRole_Returns401_IfUnknownBearerTokenProvided()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", openFullMax: 500,
            imageDeliveryChannels: deliveryChannelsForImage);
        await dbFixture.DbContext.SaveChangesAsync();
        
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/{id}/info.json");
        request.Headers.Add("Authorization", "Bearer __nonsensetoken__");
        var response = await httpClient.SendAsync(request);

        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeFalse();
        response.Headers.CacheControl.Private.Should().BeTrue();
    }
    
    [Fact]
    public async Task GetInfoJson_RestrictedImage_WithUnknownRole_Returns401_IfExpiredBearerTokenProvided()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", openFullMax: 500,
            imageDeliveryChannels: deliveryChannelsForImage);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(-1),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();
        
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/{id}/info.json");
        request.Headers.Add("Authorization", $"Bearer {authToken.Entity.BearerToken}");
        var response = await httpClient.SendAsync(request);

        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeFalse();
        response.Headers.CacheControl.Private.Should().BeTrue();
    }
    
    [Fact]
    public async Task GetInfoJson_RestrictedImage_WithUnknownRole_Returns200_AndRefreshesToken_IfValidBearerTokenProvided()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", openFullMax: 500,
            imageDeliveryChannels: deliveryChannelsForImage);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(1),
            sessionUserId: userSession.Entity.Id, ttl: 6000, lastChecked: DateTime.UtcNow.AddHours(-1));
        await dbFixture.DbContext.SaveChangesAsync();
        
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        
        var bearerToken = authToken.Entity.BearerToken;
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/{id}/info.json");
        request.Headers.Add("Authorization", $"Bearer {bearerToken}");
        var response = await httpClient.SendAsync(request);

        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeFalse();
        response.Headers.CacheControl.Private.Should().BeTrue();
        
        dbFixture.DbContext.AuthTokens.Single(t => t.BearerToken == bearerToken)
            .Expires.Should().BeAfter(DateTime.UtcNow.AddMinutes(5));
    }
}
