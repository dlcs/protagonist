using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Amazon.S3;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Orchestrator.Tests.Integration.Infrastructure;
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
    private readonly IAmazonS3 amazonS3;
    private readonly string stubAddress;
    private readonly List<ImageDeliveryChannel> deliveryChannelsForFile = new()
    {
        new ImageDeliveryChannel()
        {
            Channel = AssetDeliveryChannels.File,
            DeliveryChannelPolicyId = 4
        }
    };

    private const string ValidAuth = "Basic dW5hbWU6cHdvcmQ=";

    private readonly string validCreds =
        JsonConvert.SerializeObject(new BasicCredentials { Password = "pword", User = "uname" });

    public FileHandlingTests(ProtagonistAppFactory<Startup> factory, OrchestratorFixture orchestratorFixture)
    {
        amazonS3 = orchestratorFixture.LocalStackFixture.AWSS3ClientFactory();
        
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
        var id = AssetId.FromString($"99/1/{nameof(Get_Returns404_IfNotForDelivery)}");
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
        var id = AssetId.FromString($"99/1/{nameof(Get_Returns404_IfNotFileDeliveryChannel)}{deliveryChannel}");
        await dbFixture.DbContext.Images.AddTestAsset(id, imageDeliveryChannels: new List<ImageDeliveryChannel>()
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
        var id = AssetId.FromString($"99/1/{nameof(Get_NotOptimisedOrigin_ReturnsFileFromDLCSStorage)}");
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
    
    [Fact(Skip = "'not in dlcs storage' handling removed when switch to Yarp handling")]
    public async Task Get_NotInDlcsStorage_NotAtOrigin_Returns404()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_NotInDlcsStorage_NotAtOrigin_Returns404)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "text/plain",
            origin: $"{stubAddress}/not-found", imageDeliveryChannels: deliveryChannelsForFile);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact(Skip = "'not in dlcs storage' handling removed when switch to Yarp handling")]
    public async Task Get_NotInDlcsStorage_FallsbackToHttpOrigin_ReturnsFile()
    {
        // Note - this is for backwards compat and depends on appropriate appSetting being set
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_NotInDlcsStorage_FallsbackToHttpOrigin_ReturnsFile)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "text/plain",
            origin: $"{stubAddress}/testfile", imageDeliveryChannels: deliveryChannelsForFile);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        response.Content.Headers.ContentType.MediaType.Should().Be("text/plain");
        (await response.Content.ReadAsStringAsync()).Should().Be("from-stub");
        response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
    }
    
    [Fact(Skip = "'not in dlcs storage' handling removed when switch to Yarp handling")]
    public async Task Get_NotInDlcsStorage_FallsbackToBasicAuthHttpOrigin_ReturnsFile()
    {
        // Note - this is for backwards compat and depends on appropriate appSetting being set
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_NotInDlcsStorage_FallsbackToBasicAuthHttpOrigin_ReturnsFile)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "text/plain",
            origin: $"{stubAddress}/authfile", imageDeliveryChannels: deliveryChannelsForFile);
        await dbFixture.DbContext.CustomerOriginStrategies.AddAsync(new CustomerOriginStrategy
        {
            Credentials = validCreds, Customer = 99, Id = "basic-auth-file", 
            Strategy = OriginStrategyType.BasicHttp, Regex = $"{stubAddress}/authfile"
        });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        response.Content.Headers.ContentType.MediaType.Should().Be("text/plain");
        (await response.Content.ReadAsStringAsync()).Should().Be("secure-from-stub");
        response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
    }
    
    [Fact(Skip = "'not in dlcs storage' handling removed when switch to Yarp handling")]
    public async Task Get_NotInDlcsStorage_BasicAuthHttpOrigin_BadCredentials_Returns404()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_NotInDlcsStorage_FallsbackToBasicAuthHttpOrigin_ReturnsFile)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "application/pdf",
            origin: $"{stubAddress}/forbiddenfile", imageDeliveryChannels: deliveryChannelsForFile);
        await dbFixture.DbContext.CustomerOriginStrategies.AddAsync(new CustomerOriginStrategy
        {
            Credentials = validCreds, Customer = 99, Id = "basic-forbidden-file", 
            Strategy = OriginStrategyType.BasicHttp, Regex = $"{stubAddress}/forbiddenfile"
        });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_OptimisedOrigin_ReturnsFile()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_OptimisedOrigin_ReturnsFile)}");
        var s3Key = $"{id}/this-is-where";
        await dbFixture.DbContext.Images.AddTestAsset(id, 
            mediaType: "text/plain",
            origin: $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{s3Key}", 
            imageDeliveryChannels: deliveryChannelsForFile);
        await dbFixture.DbContext.SaveChangesAsync();

        var expectedPath = new Uri($"https://s3.amazonaws.com/{LocalStackFixture.OriginBucketName}/{s3Key}");

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.Should().Be(expectedPath);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_RequiresAuth_Returns401_IfNoCookie()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_OptimisedOrigin_ReturnsFile)}");
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
        var id = AssetId.FromString($"99/1/{nameof(Get_RequiresAuth_Returns401_IfInvalidNoCookie)}");
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
        var id = AssetId.FromString($"99/1/{nameof(Get_RequiresAuth_Returns401_IfExpiredCookie)}");
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
        var id = AssetId.FromString($"99/1/{nameof(Get_RequiresAuth_RedirectsToFile_IfCookieProvided)}");
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
        
        var expectedPath = new Uri($"https://s3.amazonaws.com/{LocalStackFixture.OriginBucketName}/{s3Key}");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"file/{id}");
        request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
        var response = await httpClient.SendAsync(request);
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Set-Cookie");
        proxyResponse.Uri.Should().Be(expectedPath);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }

    private static void ConfigureStubbery(OrchestratorFixture orchestratorFixture)
    {
        orchestratorFixture.ApiStub.Get("/testfile", (request, args) => "from-stub")
            .Header("Content-Type", "text/plain");

        orchestratorFixture.ApiStub.Get("/authfile", (request, args) => "secure-from-stub")
            .Header("Content-Type", "text/plain")
            .IfHeader("Authorization", ValidAuth);

        orchestratorFixture.ApiStub.Get("/forbiddenfile", (request, args) => new ForbidResult());
    }
}