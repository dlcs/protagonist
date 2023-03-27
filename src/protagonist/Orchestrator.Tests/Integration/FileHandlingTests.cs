using System.Net;
using System.Net.Http;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Core.Types;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;

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
            .WithConfigValue("StreamMissingFileFromOrigin", "true")
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
    public async Task Get_Returns404_IfNoOrigin()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_Returns404_IfNotForDelivery)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "", deliveryChannels: new[] { "file" });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Returns404_IfNotForDelivery()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_Returns404_IfNotForDelivery)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, notForDelivery: true, deliveryChannels: new[] { "file" });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Theory]
    [InlineData("iiif-img")]
    [InlineData("iiif-av")]
    public async Task Get_Returns404_IfNotFileDeliveryChannel(string deliveryChannel)
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_Returns404_IfNotFileDeliveryChannel)}{deliveryChannel}");
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { deliveryChannel });
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
            origin: $"{stubAddress}/testfile", deliveryChannels: new[] { "file" });
        await dbFixture.DbContext.SaveChangesAsync();

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = LocalStackFixture.StorageBucketName,
            Key = $"{id}/original",
            ContentBody = nameof(Get_NotOptimisedOrigin_ReturnsFileFromDLCSStorage),
            ContentType = "text/plain"
        });

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        response.Content.Headers.ContentType.MediaType.Should().Be("text/plain");
        (await response.Content.ReadAsStringAsync()).Should().Be(nameof(Get_NotOptimisedOrigin_ReturnsFileFromDLCSStorage));
        response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public async Task Get_NotInDlcsStorage_NotAtOrigin_Returns404()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_NotInDlcsStorage_NotAtOrigin_Returns404)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "text/plain",
            origin: $"{stubAddress}/not-found", deliveryChannels: new[] { "file" });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_NotInDlcsStorage_FallsbackToHttpOrigin_ReturnsFile()
    {
        // Note - this is for backwards compat and depends on appropriate appSetting being set
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_NotInDlcsStorage_FallsbackToHttpOrigin_ReturnsFile)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "text/plain",
            origin: $"{stubAddress}/testfile", deliveryChannels: new[] { "file" });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        response.Content.Headers.ContentType.MediaType.Should().Be("text/plain");
        (await response.Content.ReadAsStringAsync()).Should().Be("from-stub");
        response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public async Task Get_NotInDlcsStorage_FallsbackToBasicAuthHttpOrigin_ReturnsFile()
    {
        // Note - this is for backwards compat and depends on appropriate appSetting being set
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_NotInDlcsStorage_FallsbackToBasicAuthHttpOrigin_ReturnsFile)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "text/plain",
            origin: $"{stubAddress}/authfile", deliveryChannels: new[] { "file" });
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
    
    [Fact]
    public async Task Get_NotInDlcsStorage_BasicAuthHttpOrigin_BadCredentials_Returns404()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_NotInDlcsStorage_FallsbackToBasicAuthHttpOrigin_ReturnsFile)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, mediaType: "application/pdf",
            origin: $"{stubAddress}/forbiddenfile", deliveryChannels: new[] { "file" });
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
            deliveryChannels: new[] { "file" });
        await dbFixture.DbContext.SaveChangesAsync();
        
        // As this is the origin-bucket it will be hardoded origin so no need to add
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = LocalStackFixture.OriginBucketName,
            Key = s3Key,
            ContentBody = nameof(Get_OptimisedOrigin_ReturnsFile),
            ContentType = "text/plain"
        });

        // Act
        var response = await httpClient.GetAsync($"file/{id}");
        
        // Assert
        response.Content.Headers.ContentType.MediaType.Should().Be("text/plain");
        (await response.Content.ReadAsStringAsync()).Should().Be(nameof(Get_OptimisedOrigin_ReturnsFile));
        response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
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