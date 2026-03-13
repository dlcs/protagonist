using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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
/// Tests of all /adjuncts/ requests
/// </summary>
[Trait("Category", "Integration")]
[Collection(OrchestratorCollection.CollectionName)]
public class AdjunctHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
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
    
    public AdjunctHandlingTests(ProtagonistAppFactory<Startup> factory, OrchestratorFixture orchestratorFixture)
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
        const string path = "adjuncts/1/1/my-file.pdf/fnord";

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
        const string path = "adjuncts/1/1/my-file.pdf/quark";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_UnknownSpace_Returns404()
    {
        // Arrange
        const string path = "adjuncts/99/5/my-file.pdf/plum";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_UnknownImage_Returns404()
    {
        // Arrange
        const string path = "adjuncts/99/1/my-file.pdf/quack";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_NotOptimisedOrigin_ReturnsAdjunctFromDLCSStorage()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        const string adjunctId = nameof(Get_NotOptimisedOrigin_ReturnsAdjunctFromDLCSStorage);
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "text/plain",
            origin: $"{stubAddress}/testfile", imageDeliveryChannels: deliveryChannelsForFile);
        await dbFixture.DbContext.Adjuncts.AddAsync(new()
        {
            AssetId = id, Id = adjunctId, Created = DateTime.UtcNow, Origin = $"{stubAddress}/testadjunct",
            IIIFLink = IIIFLinkType.SeeAlso, MediaType = "text/plain", Finished = DateTime.UtcNow, Type = "type",
            Ingesting = false, Size = 100L
        });
        
        await dbFixture.DbContext.SaveChangesAsync();

        var expectedPath = new Uri($"https://protagonist-storage.s3.eu-west-1.amazonaws.com/{id}/adjuncts/{adjunctId}");

        // Act
        var response = await httpClient.GetAsync($"adjuncts/{id}/{adjunctId}");
        
        // Assert
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        proxyResponse.Uri.Should().Be(expectedPath);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_OptimisedOrigin_ReturnsFile_AssetNotOptimized()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        var adjunctId = nameof(Get_OptimisedOrigin_ReturnsFile_AssetNotOptimized);
        var s3Key = $"{id}/this-is-where";
        var adjS3Key = $"{s3Key}/adjuncts/{adjunctId}";
        await dbFixture.DbContext.Images.AddTestAsset(id, 
            mediaType: "text/plain",
            origin: $"{stubAddress}/testfile", 
            imageDeliveryChannels: deliveryChannelsForFile);
        await dbFixture.DbContext.Adjuncts.AddAsync(new()
        {
            AssetId = id, Id = adjunctId, Created = DateTime.UtcNow, Origin = $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{adjS3Key}",
            IIIFLink = IIIFLinkType.SeeAlso, MediaType = "text/plain", Finished = DateTime.UtcNow, Type = "type",
            Ingesting = false, Size = 100L
        });
        
        await dbFixture.DbContext.SaveChangesAsync();

        var expectedPathRegex = GetExpectedPathRegex(adjS3Key);

        // Act
        var response = await httpClient.GetAsync($"adjuncts/{id}/{adjunctId}");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.ToString().Should().MatchRegex(expectedPathRegex);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_OptimisedOrigin_ReturnsFile()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        var adjunctId = nameof(Get_OptimisedOrigin_ReturnsFile);
        var s3Key = $"{id}/this-is-where";
        var adjS3Key = $"{s3Key}/adjuncts/{adjunctId}";
        await dbFixture.DbContext.Images.AddTestAsset(id, 
            mediaType: "text/plain",
            origin: $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{s3Key}", 
            imageDeliveryChannels: deliveryChannelsForFile);
        await dbFixture.DbContext.Adjuncts.AddAsync(new()
        {
            AssetId = id, Id = adjunctId, Created = DateTime.UtcNow, Origin = $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{adjS3Key}",
            IIIFLink = IIIFLinkType.SeeAlso, MediaType = "text/plain", Finished = DateTime.UtcNow, Type = "type",
            Ingesting = false, Size = 100L
        });
        
        await dbFixture.DbContext.SaveChangesAsync();

        var expectedPathRegex = GetExpectedPathRegex(adjS3Key);

        // Act
        var response = await httpClient.GetAsync($"adjuncts/{id}/{adjunctId}");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
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
        
        orchestratorFixture.ApiStub.Get("/testadjunct", (_, _) => "from-stub-as-well")
            .Header("Content-Type", "text/plain");
    }
}
