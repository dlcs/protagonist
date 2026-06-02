using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using DLCS.Repository;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Settings;
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
    private readonly ProtagonistAppFactory<Startup> appFactory;
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;
    private readonly string stubAddress;
    private readonly IAmazonS3 amazonS3;
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
        appFactory = factory;
        var dbFixture1 = orchestratorFixture.DbFixture;
        dbContext = dbFixture1.DbContext;
        stubAddress = orchestratorFixture.ApiStub.Address;
        amazonS3 = orchestratorFixture.LocalStackFixture.AWSS3ClientFactory();
        httpClient = factory
            .WithConnectionString(dbFixture1.ConnectionString)
            .WithLocalStack(orchestratorFixture.LocalStackFixture)
            .WithTestServices(services =>
            {
                services
                    .AddSingleton<IForwarderHttpClientFactory, TestProxyHttpClientFactory>()
                    .AddSingleton<IHttpForwarder, TestProxyForwarder>()
                    .AddSingleton<TestProxyHandler>();
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        dbFixture1.CleanUp();
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
        await dbContext.Images.AddTestAsset(id, mediaType: "text/plain",
            origin: $"{stubAddress}/testfile", imageDeliveryChannels: deliveryChannelsForFile)
            .WithTestAdjunct(adjunctId, origin:$"{stubAddress}/testadjunct");
        
        await dbContext.SaveChangesAsync();

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
        await dbContext.Images.AddTestAsset(id, 
            mediaType: "text/plain",
            origin: $"{stubAddress}/testfile", 
            imageDeliveryChannels: deliveryChannelsForFile)
            .WithTestAdjunct(adjunctId, origin:$"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{adjS3Key}");
      
        
        await dbContext.SaveChangesAsync();

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
        await dbContext.Images.AddTestAsset(id,
                mediaType: "text/plain",
                origin: $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{s3Key}",
                imageDeliveryChannels: deliveryChannelsForFile)
            .WithTestAdjunct(adjunctId,
                origin: $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{adjS3Key}");
        
        await dbContext.SaveChangesAsync();

        var expectedPathRegex = GetExpectedPathRegex(adjS3Key);

        // Act
        var response = await httpClient.GetAsync($"adjuncts/{id}/{adjunctId}");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.ToString().Should().MatchRegex(expectedPathRegex);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_AnnotationAdjunct_RewritesTopLevelId()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        var adjunctId = nameof(Get_AnnotationAdjunct_RewritesTopLevelId);
        var s3Key = $"{id}/adjuncts/{adjunctId}";
        var origin = $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{s3Key}";

        const string originalJson = """
            {
              "id": "https://old.example.org/annotation-page/1",
              "type": "AnnotationPage",
              "items": []
            }
            """;

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = LocalStackFixture.OriginBucketName,
            Key = s3Key,
            ContentBody = originalJson,
            ContentType = "application/json"
        });

        await dbContext.Images
            .AddTestAsset(id, mediaType: "text/plain", origin: $"{stubAddress}/testfile",
                imageDeliveryChannels: deliveryChannelsForFile)
            .WithTestAdjunct(adjunctId, mediaType: "application/json", iiifLinkType: IIIFLinkType.Annotations,
                origin: origin);
        await dbContext.SaveChangesAsync();

        var requestPath = $"adjuncts/{id}/{adjunctId}";
        var expectedId = $"http://localhost/{requestPath}";

        // Act — include query params to verify they are excluded from the rewritten id
        var response = await httpClient.GetAsync($"{requestPath}?page=2&format=json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("id").GetString().Should().Be(expectedId);
        doc.RootElement.GetProperty("type").GetString().Should().Be("AnnotationPage");
    }

    [Fact]
    public async Task Get_AnnotationAdjunct_NonOptimisedOrigin_RewritesTopLevelId()
    {
        // Arrange — adjunct origin is not an S3 URL, so OptimisedOrigin=false and the orchestrator
        // fetches from DLCS storage rather than directly from the origin.
        var id = AssetIdGenerator.GetAssetId();
        const string adjunctId = nameof(Get_AnnotationAdjunct_NonOptimisedOrigin_RewritesTopLevelId);
        var s3Key = $"{id}/adjuncts/{adjunctId}";

        const string originalJson = """
            {
              "id": "https://old.example.org/annotation-page/1",
              "type": "AnnotationPage",
              "items": []
            }
            """;

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = LocalStackFixture.StorageBucketName,
            Key = s3Key,
            ContentBody = originalJson,
            ContentType = "application/json"
        });

        await dbContext.Images
            .AddTestAsset(id, mediaType: "text/plain", origin: $"{stubAddress}/testfile",
                imageDeliveryChannels: deliveryChannelsForFile)
            .WithTestAdjunct(adjunctId, mediaType: "application/json", iiifLinkType: IIIFLinkType.Annotations,
                origin: $"{stubAddress}/testadjunct");
        await dbContext.SaveChangesAsync();

        var requestPath = $"adjuncts/{id}/{adjunctId}";
        var expectedId = $"http://localhost/{requestPath}";

        // Act
        var response = await httpClient.GetAsync(requestPath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("id").GetString().Should().Be(expectedId);
        doc.RootElement.GetProperty("type").GetString().Should().Be("AnnotationPage");
    }

    [Theory]
    [InlineData("my-proxy.com", "const_value/{0}/{1}/{2}")]
    [InlineData("non-versioned.com", "adjuncts/{0}/{1}/{2}")]
    [InlineData("versioned.com", "adj/_{1}/{2}")]
    public async Task Get_AnnotationAdjunct_RewrittenId_RespectsPathTemplate(string host, string pathFormat)
    {
        // {0}=customer, {1}=asset, {2}=adjunctId. All three templates drop {space} and produce a path
        // that Scheme+Host+Request.Path alone could never generate, verifying IAssetPathGenerator is used.
        var id = AssetIdGenerator.GetAssetId();
        const string adjunctId = "path-template-adjunct";
        var s3Key = $"{id}/adjuncts/{adjunctId}";
        var origin = $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{s3Key}";

        const string originalJson = """
            {
              "id": "https://old.example.org/annotation-page/1",
              "type": "AnnotationPage",
              "items": []
            }
            """;

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = LocalStackFixture.OriginBucketName,
            Key = s3Key,
            ContentBody = originalJson,
            ContentType = "application/json"
        });

        await dbContext.Images
            .AddTestAsset(id, mediaType: "text/plain", origin: $"{stubAddress}/testfile",
                imageDeliveryChannels: deliveryChannelsForFile)
            .WithTestAdjunct(adjunctId, mediaType: "application/json", iiifLinkType: IIIFLinkType.Annotations,
                origin: origin);
        await dbContext.SaveChangesAsync();

        var expectedId = $"http://{host}/{string.Format(pathFormat, id.Customer, id.Asset, adjunctId)}";

        var request = new HttpRequestMessage(HttpMethod.Get, $"adjuncts/{id}/{adjunctId}");
        request.Headers.Host = host;

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("id").GetString().Should().Be(expectedId);
    }

    [Fact]
    public async Task Get_AnnotationAdjunct_Returns404_WhenS3ObjectMissing()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        const string adjunctId = nameof(Get_AnnotationAdjunct_Returns404_WhenS3ObjectMissing);
        var origin = $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{id}/adjuncts/{adjunctId}-missing";

        await dbContext.Images
            .AddTestAsset(id, mediaType: "text/plain", origin: $"{stubAddress}/testfile",
                imageDeliveryChannels: deliveryChannelsForFile)
            .WithTestAdjunct(adjunctId, mediaType: "application/json", iiifLinkType: IIIFLinkType.Annotations,
                origin: origin);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"adjuncts/{id}/{adjunctId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_SeeAlsoAdjunct_IsProxied_NotRewritten()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        const string adjunctId = nameof(Get_SeeAlsoAdjunct_IsProxied_NotRewritten);
        var s3Key = $"{id}/this-is-see-also";
        var adjS3Key = $"{s3Key}/adjuncts/{adjunctId}";

        await dbContext.Images
            .AddTestAsset(id,
                mediaType: "text/plain",
                origin: $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{s3Key}",
                imageDeliveryChannels: deliveryChannelsForFile)
            .WithTestAdjunct(adjunctId, iiifLinkType: IIIFLinkType.SeeAlso,
                origin: $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{adjS3Key}");
        await dbContext.SaveChangesAsync();

        var expectedPathRegex = GetExpectedPathRegex(adjS3Key);

        // Act
        var response = await httpClient.GetAsync($"adjuncts/{id}/{adjunctId}");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();

        // Assert — still goes through the YARP proxy, not the id-rewriter path
        proxyResponse.Uri.ToString().Should().MatchRegex(expectedPathRegex);
    }

    [Fact]
    public async Task Get_AnnotationAdjunct_Returns500_WhenJsonIsMalformed()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        const string adjunctId = nameof(Get_AnnotationAdjunct_Returns500_WhenJsonIsMalformed);
        var s3Key = $"{id}/adjuncts/{adjunctId}";
        var origin = $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{s3Key}";

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = LocalStackFixture.OriginBucketName,
            Key = s3Key,
            ContentBody = "{ this is not valid json ]",
            ContentType = "application/json"
        });

        await dbContext.Images
            .AddTestAsset(id, mediaType: "text/plain", origin: $"{stubAddress}/testfile",
                imageDeliveryChannels: deliveryChannelsForFile)
            .WithTestAdjunct(adjunctId, mediaType: "application/json", iiifLinkType: IIIFLinkType.Annotations,
                origin: origin);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"adjuncts/{id}/{adjunctId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Get_AnnotationAdjunct_Returns500_WhenExceedsMaxSizeBytes()
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();
        const string adjunctId = nameof(Get_AnnotationAdjunct_Returns500_WhenExceedsMaxSizeBytes);
        var s3Key = $"{id}/adjuncts/{adjunctId}";
        var origin = $"http://{LocalStackFixture.OriginBucketName}.s3.amazonaws.com/{s3Key}";

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = LocalStackFixture.OriginBucketName,
            Key = s3Key,
            ContentBody = """{ "id": "https://old.example.org/1", "type": "AnnotationPage", "items": [] }""",
            ContentType = "application/json"
        });

        await dbContext.Images
            .AddTestAsset(id, mediaType: "text/plain", origin: $"{stubAddress}/testfile",
                imageDeliveryChannels: deliveryChannelsForFile)
            .WithTestAdjunct(adjunctId, mediaType: "application/json", iiifLinkType: IIIFLinkType.Annotations,
                origin: origin);
        await dbContext.SaveChangesAsync();

        // Use a client configured with a 1-byte size limit so any real object is rejected
        using var sizeLimitedClient = appFactory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.PostConfigure<OrchestratorSettings>(s => s.MaxAdjunctSizeBytes = 1)))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Act
        var response = await sizeLimitedClient.GetAsync($"adjuncts/{id}/{adjunctId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
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
