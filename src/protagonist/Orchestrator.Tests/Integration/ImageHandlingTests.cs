using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Auth.Entities;
using IIIF;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using IIIF.Serialisation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Orchestrator.Assets;
using Orchestrator.Features.Images.ImageServer;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers;
using Test.Helpers.Integration;
using Yarp.ReverseProxy.Forwarder;
using Version = IIIF.ImageApi.Version;

namespace Orchestrator.Tests.Integration;

/// <summary>
/// Test of all requests handled by custom iiif-img handling
/// </summary>
[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class ImageHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;
    private readonly IAmazonS3 amazonS3;
    private readonly FakeImageOrchestrator orchestrator = new();
    private const string SizesJsonContent = "{\"o\":[[800,800],[400,400],[200,200]],\"a\":[]}";

    public ImageHandlingTests(ProtagonistAppFactory<Startup> factory, StorageFixture storageFixture)
    {
        dbFixture = storageFixture.DbFixture;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithConfigValue("OrchestrateOnInfoJson", "true")
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
            .CreateClient(new WebApplicationFactoryClientOptions {AllowAutoRedirect = false});
        
        dbFixture.CleanUp();
    }
    
    [Theory]
    [InlineData("/iiif-img/1/1")]
    [InlineData("/iiif-img/1/1/info.json")]
    [InlineData("/iiif-img/1/1/full/1000,/0/default.jpg")]
    public async Task Options_Returns200_WithCorsHeaders(string path)
    {
        // Arrange
        var corsHeaders = new[]
        {
            "Access-Control-Allow-Origin", "Access-Control-Allow-Headers", "Access-Control-Allow-Methods"
        };
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKeys(corsHeaders);
    }
    
    [Theory]
    [InlineData("/iiif-img/2/1/image", "/iiif-img/2/1/image/info.json")]
    [InlineData("/iiif-img/2/1/image/", "/iiif-img/2/1/image/info.json")]
    [InlineData("/iiif-img/2/1/image?x=y", "/iiif-img/2/1/image/info.json")]
    [InlineData("/iiif-img/2/1/image/?x=y", "/iiif-img/2/1/image/info.json")]
    [InlineData("/iiif-img/display-name/1/image", "/iiif-img/display-name/1/image/info.json")]
    [InlineData("/iiif-img/display-name/1/image/", "/iiif-img/display-name/1/image/info.json")]
    [InlineData("/iiif-img/display-name/1/image?x=y", "/iiif-img/display-name/1/image/info.json")]
    [InlineData("/iiif-img/display-name/1/image/?x=y", "/iiif-img/display-name/1/image/info.json")]
    [InlineData("/iiif-img/v2/2/1/image", "/iiif-img/v2/2/1/image/info.json")]
    [InlineData("/iiif-img/v2/2/1/image/", "/iiif-img/v2/2/1/image/info.json")]
    [InlineData("/iiif-img/v2/2/1/image?x=y", "/iiif-img/v2/2/1/image/info.json")]
    [InlineData("/iiif-img/v2/2/1/image/?x=y", "/iiif-img/v2/2/1/image/info.json")]
    [InlineData("/iiif-img/v2/display-name/1/image", "/iiif-img/v2/display-name/1/image/info.json")]
    [InlineData("/iiif-img/v2/display-name/1/image/", "/iiif-img/v2/display-name/1/image/info.json")]
    [InlineData("/iiif-img/v2/display-name/1/image?x=y", "/iiif-img/v2/display-name/1/image/info.json")]
    [InlineData("/iiif-img/v2/display-name/1/image/?x=y", "/iiif-img/v2/display-name/1/image/info.json")]
    public async Task Get_ImageRoot_RedirectsToInfoJson(string path, string expected)
    {
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("/iiif-img/2/1/image", "/const_value/2/image/info.json")]
    [InlineData("/iiif-img/2/1/image/", "/const_value/2/image/info.json")]
    [InlineData("/iiif-img/2/1/image?x=y", "/const_value/2/image/info.json")]
    [InlineData("/iiif-img/2/1/image/?x=y", "/const_value/2/image/info.json")]
    [InlineData("/iiif-img/display-name/1/image", "/const_value/display-name/image/info.json")]
    [InlineData("/iiif-img/display-name/1/image/", "/const_value/display-name/image/info.json")]
    [InlineData("/iiif-img/display-name/1/image?x=y", "/const_value/display-name/image/info.json")]
    [InlineData("/iiif-img/display-name/1/image/?x=y", "/const_value/display-name/image/info.json")]
    [InlineData("/iiif-img/v2/2/1/image", "/const_value/v2/2/image/info.json")]
    [InlineData("/iiif-img/v2/2/1/image/", "/const_value/v2/2/image/info.json")]
    [InlineData("/iiif-img/v2/2/1/image?x=y", "/const_value/v2/2/image/info.json")]
    [InlineData("/iiif-img/v2/2/1/image/?x=y", "/const_value/v2/2/image/info.json")]
    [InlineData("/iiif-img/v3/2/1/image", "/const_value/2/image/info.json")]
    [InlineData("/iiif-img/v3/2/1/image/", "/const_value/2/image/info.json")]
    [InlineData("/iiif-img/v3/2/1/image?x=y", "/const_value/2/image/info.json")]
    [InlineData("/iiif-img/v3/2/1/image/?x=y", "/const_value/2/image/info.json")]
    [InlineData("/iiif-img/v2/display-name/1/image", "/const_value/v2/display-name/image/info.json")]
    [InlineData("/iiif-img/v2/display-name/1/image/", "/const_value/v2/display-name/image/info.json")]
    [InlineData("/iiif-img/v2/display-name/1/image?x=y", "/const_value/v2/display-name/image/info.json")]
    [InlineData("/iiif-img/v2/display-name/1/image/?x=y", "/const_value/v2/display-name/image/info.json")]
    [InlineData("/iiif-img/v3/display-name/1/image", "/const_value/display-name/image/info.json")]
    [InlineData("/iiif-img/v3/display-name/1/image/", "/const_value/display-name/image/info.json")]
    [InlineData("/iiif-img/v3/display-name/1/image?x=y", "/const_value/display-name/image/info.json")]
    [InlineData("/iiif-img/v3/display-name/1/image/?x=y", "/const_value/display-name/image/info.json")]
    public async Task Get_ImageRoot_RedirectsToInfoJson_CustomPathValues(string path, string expected)
    {
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Host", "my-proxy.com");
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("/iiif-img/v3/2/1/image", "/iiif-img/2/1/image/info.json")]
    [InlineData("/iiif-img/v3/2/1/image?x=y", "/iiif-img/2/1/image/info.json")]
    [InlineData("/iiif-img/v3/2/1/image/", "/iiif-img/2/1/image/info.json")]
    [InlineData("/iiif-img/v3/2/1/image/?x=y", "/iiif-img/2/1/image/info.json")]
    [InlineData("/iiif-img/v3/display-name/1/image", "/iiif-img/display-name/1/image/info.json")]
    [InlineData("/iiif-img/v3/display-name/1/image?x=y", "/iiif-img/display-name/1/image/info.json")]
    [InlineData("/iiif-img/v3/display-name/1/image/", "/iiif-img/display-name/1/image/info.json")]
    [InlineData("/iiif-img/v3/display-name/1/image/?x=y", "/iiif-img/display-name/1/image/info.json")]
    public async Task Get_ImageRoot_RedirectsToCanonicalInfoJson_IfRequestingCanonicalVersion(string path, string expected)
    {
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location.Should().Be(expected);
    }
    
    [Fact]
    public async Task GetInfoJson_Correct_ViaDisplayName()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJsonV2_Correct_ViaDirectPath_NotInS3)}");
        var namedId = $"test/1/{nameof(GetInfoJsonV2_Correct_ViaDirectPath_NotInS3)}";
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = sizesJsonContent
        });
        await dbFixture.DbContext.SaveChangesAsync();
        var expectedSizes = new List<Size> { new(800, 800), new(400, 400), new(200, 200) };

        // Act
        var response = await httpClient.GetAsync($"iiif-img/v2/{namedId}/info.json");
        
        // Assert
        // Verify correct info.json returned
        var jsonResponse = (await response.Content.ReadAsStreamAsync()).FromJsonStream<ImageService2>();
        jsonResponse.Id.Should().Be($"http://localhost/iiif-img/v2/{namedId}");
        jsonResponse.Context.ToString().Should().Be("http://iiif.io/api/image/2/context.json");
        jsonResponse.Sizes.Should().BeEquivalentTo(expectedSizes);

        // With correct headers/status
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        response.Content.Headers.ContentType.ToString().Should()
            .Be("application/json", "application/json unless Accept header specified");
    }

    [Fact]
    public async Task GetInfoJsonV2_Correct_ViaDirectPath_NotInS3()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJsonV2_Correct_ViaDirectPath_NotInS3)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        await dbFixture.DbContext.SaveChangesAsync();
        var expectedSizes = new List<Size> { new(800, 800), new(400, 400), new(200, 200) };

        // Act
        var response = await httpClient.GetAsync($"iiif-img/v2/{id}/info.json");
        
        // Assert
        // Verify correct info.json returned
        var jsonResponse = (await response.Content.ReadAsStreamAsync()).FromJsonStream<ImageService2>();
        jsonResponse.Id.Should().Be($"http://localhost/iiif-img/v2/{id}");
        jsonResponse.Context.ToString().Should().Be("http://iiif.io/api/image/2/context.json");
        jsonResponse.Sizes.Should().BeEquivalentTo(expectedSizes);

        // With correct headers/status
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        response.Content.Headers.ContentType.ToString().Should()
            .Be("application/json", "application/json unless Accept header specified");
        
        // And a copy was put in S3 for future requests
        var s3InfoJsonObject =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName, $"{id}/info/Cantaloupe/v2/info.json");
        var s3InfoJson = JObject.Parse(s3InfoJsonObject.ResponseStream.GetContentString());
        s3InfoJson["@id"].ToString().Should()
            .NotBe($"http://localhost/iiif-img/v2/{id}", "Stored Id is placeholder only");
        s3InfoJson["@context"].ToString().Should().Be("http://iiif.io/api/image/2/context.json");
    }
    
    [Fact]
    public async Task GetInfoJsonV2_ReturnsImageServerSizes_IfS3GetFails()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJsonV2_ReturnsImageServerSizes_IfS3GetFails)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Sizes from FakeImageServerClient
        var expectedSizes = new List<Size> { new(100, 100), new(25, 25), new(1, 1) };

        // Act
        var response = await httpClient.GetAsync($"iiif-img/v2/{id}/info.json");
        
        // Assert
        // Verify correct info.json returned
        var jsonResponse = (await response.Content.ReadAsStreamAsync()).FromJsonStream<ImageService2>();
        jsonResponse.Id.Should().Be($"http://localhost/iiif-img/v2/{id}");
        jsonResponse.Context.ToString().Should().Be("http://iiif.io/api/image/2/context.json");
        jsonResponse.Sizes.Should().BeEquivalentTo(expectedSizes);

        // With correct headers/status
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        response.Content.Headers.ContentType.ToString().Should()
            .Be("application/json", "application/json unless Accept header specified");
        
        // And a copy was put in S3 for future requests
        var s3InfoJsonObject =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName, $"{id}/info/Cantaloupe/v2/info.json");
        var s3InfoJson = JObject.Parse(s3InfoJsonObject.ResponseStream.GetContentString());
        s3InfoJson["@id"].ToString().Should()
            .NotBe($"http://localhost/iiif-img/v2/{id}", "Stored Id is placeholder only");
        s3InfoJson["@context"].ToString().Should().Be("http://iiif.io/api/image/2/context.json");
    }
    
    [Fact]
    public async Task GetInfoJsonV2_Correct_ViaDirectPath_NotInS3_CustomPathRules()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJsonV2_Correct_ViaDirectPath_NotInS3_CustomPathRules)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        await dbFixture.DbContext.SaveChangesAsync();
        var expectedSizes = new List<Size> { new(800, 800), new(400, 400), new(200, 200) };

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/v2/{id}/info.json");
        request.Headers.Add("Host", "my-proxy.com");
        var response = await httpClient.SendAsync(request);
        
        // Assert
        // Verify correct info.json returned
        var jsonResponse = (await response.Content.ReadAsStreamAsync()).FromJsonStream<ImageService2>();
        jsonResponse.Id.Should()
            .Be($"http://my-proxy.com/const_value/v2/99/{nameof(GetInfoJsonV2_Correct_ViaDirectPath_NotInS3_CustomPathRules)}");
        jsonResponse.Context.ToString().Should().Be("http://iiif.io/api/image/2/context.json");
        jsonResponse.Sizes.Should().BeEquivalentTo(expectedSizes);

        // With correct headers/status
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        response.Content.Headers.ContentType.ToString().Should()
            .Be("application/json", "application/json unless Accept header specified");
        
        // And a copy was put in S3 for future requests
        var s3InfoJsonObject =
            await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName, $"{id}/info/Cantaloupe/v2/info.json");
        var s3InfoJson = JObject.Parse(s3InfoJsonObject.ResponseStream.GetContentString());
        s3InfoJson["@id"].ToString().Should()
            .NotBe(
                $"http://my-proxy.com/const_value/v2/99/{nameof(GetInfoJsonV2_Correct_ViaDirectPath_NotInS3_CustomPathRules)}",
                "Stored Id is placeholder only");
        s3InfoJson["@context"].ToString().Should().Be("http://iiif.io/api/image/2/context.json");
    }
    
    [Fact]
    public async Task GetInfoJsonV2_Correct_ViaDirectPath_AlreadyInS3()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJsonV2_Correct_ViaDirectPath_AlreadyInS3)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/info/Cantaloupe/v2/info.json",
            BucketName = LocalStackFixture.StorageBucketName,
            ContentBody = "{\"@context\": \"_this_proves_s3_origin_\"}"
        });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"iiif-img/v2/{id}/info.json");
        
        // Assert
        // Verify correct info.json returned
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should().Be($"http://localhost/iiif-img/v2/{id}");
        jsonResponse["@context"].ToString().Should().Be("_this_proves_s3_origin_");

        // With correct headers/status
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        response.Content.Headers.ContentType.ToString().Should()
            .Be("application/json", "application/json unless Accept header specified");
    }
    
    [Fact]
    public async Task GetInfoJsonV2_Correct_ViaDirectPath_AlreadyInS3_CustomPathRules()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJsonV2_Correct_ViaDirectPath_AlreadyInS3_CustomPathRules)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/info/Cantaloupe/v2/info.json",
            BucketName = LocalStackFixture.StorageBucketName,
            ContentBody = "{\"@context\": \"_this_proves_s3_origin_\"}"
        });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/v2/{id}/info.json");
        request.Headers.Add("Host", "my-proxy.com");
        var response = await httpClient.SendAsync(request);
        
        // Assert
        // Verify correct info.json returned
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should()
            .Be($"http://my-proxy.com/const_value/v2/99/{nameof(GetInfoJsonV2_Correct_ViaDirectPath_AlreadyInS3_CustomPathRules)}");
        jsonResponse["@context"].ToString().Should().Be("_this_proves_s3_origin_");

        // With correct headers/status
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        response.Content.Headers.ContentType.ToString().Should()
            .Be("application/json", "application/json unless Accept header specified");
    }
    
    [Fact]
    public async Task GetInfoJsonV2_Correct_ViaConneg()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJsonV2_Correct_ViaConneg)}");
        const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/image/2/context.json\"";
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/{id}/info.json");
        request.Headers.Add("Accept", iiif2);
        var response = await httpClient.SendAsync(request);

        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should().Be("http://localhost/iiif-img/99/1/GetInfoJsonV2_Correct_ViaConneg");
        jsonResponse["@context"].ToString().Should().Be("http://iiif.io/api/image/2/context.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        response.Content.Headers.ContentType.ToString().Should()
            .Be("application/ld+json", "application/ld+json as Accept header specified");
    }
    
    [Fact]
    public async Task GetInfoJsonV3_RedirectsToCanonical()
    {
        // Arrange
        var id = $"99/1/{nameof(GetInfoJsonV3_RedirectsToCanonical)}";

        // Act
        var response = await httpClient.GetAsync($"iiif-img/v3/{id}/info.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().Be($"/iiif-img/{id}/info.json");
    }
    
    [Fact]
    public async Task GetInfoJsonV3_Correct_ViaConneg_CustomPathRules()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJsonV3_Correct_ViaConneg_CustomPathRules)}"); 
        const string iiif3 = "application/ld+json; profile=\"http://iiif.io/api/image/3/context.json\"";
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        await dbFixture.DbContext.SaveChangesAsync();
        var expectedSizes = new List<Size> { new(800, 800), new(400, 400), new(200, 200) };
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/{id}/info.json");
        request.Headers.Add("Accept", iiif3);
        request.Headers.Add("Host", "my-proxy.com");
        var response = await httpClient.SendAsync(request);

        // Assert
        var jsonResponse = (await response.Content.ReadAsStreamAsync()).FromJsonStream<ImageService3>();
        jsonResponse.Id.Should()
            .Be($"http://my-proxy.com/const_value/99/{nameof(GetInfoJsonV3_Correct_ViaConneg_CustomPathRules)}");
        jsonResponse.Context.ToString().Should().Be("http://iiif.io/api/image/3/context.json");
        jsonResponse.Sizes.Should().BeEquivalentTo(expectedSizes);
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        response.Content.Headers.ContentType.ToString().Should().Be(iiif3);
    }
    
    [Fact]
    public async Task GetInfoJsonV3_Correct_ViaConneg()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJsonV3_Correct_ViaConneg)}");
        const string iiif3 = "application/ld+json; profile=\"http://iiif.io/api/image/3/context.json\"";
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/{id}/info.json");
        request.Headers.Add("Accept", iiif3);
        var response = await httpClient.SendAsync(request);

        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be("http://localhost/iiif-img/99/1/GetInfoJsonV3_Correct_ViaConneg");
        jsonResponse["@context"].ToString().Should().Be("http://iiif.io/api/image/3/context.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        response.Content.Headers.ContentType.ToString().Should().Be(iiif3);
    }

    [Fact]
    public async Task GetInfoJson_OpenImage_Correct()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJson_OpenImage_Correct)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/info.json");

        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be("http://localhost/iiif-img/99/1/GetInfoJson_OpenImage_Correct");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }
    
    [Fact]
    public async Task GetInfoJson_OrchestratesImage_IfServedFromS3()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJson_OrchestratesImage_IfServedFromS3)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/info/Cantaloupe/v3/info.json",
            BucketName = LocalStackFixture.StorageBucketName,
            ContentBody = "{\"id\": \"whatever\", \"type\": \"ImageService3\", \"context\": \"_this_proves_s3_origin_\"}"
        });
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        await httpClient.GetAsync($"iiif-img/{id}/info.json");

        // Assert
        FakeImageOrchestrator.OrchestratedImages.Should().Contain(id);
    }
    
    [Fact]
    public async Task GetInfoJson_DoesNotOrchestratesImage_IfServedFromImageServer()
    {
        // NOTE: This is testing that the infojson handler orchestrates - the image will have been orchestrated to allow
        // the imageserver to serve but this isn't caught in these tests 
        
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJson_DoesNotOrchestratesImage_IfServedFromImageServer)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        await httpClient.GetAsync($"iiif-img/{id}/info.json");

        // Assert
        FakeImageOrchestrator.OrchestratedImages.Should().NotContain(id);
    }
    
    [Fact]
    public async Task GetInfoJson_DoesNotOrchestratesImage_IfQueryParamPassed()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJson_DoesNotOrchestratesImage_IfQueryParamPassed)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        await httpClient.GetAsync($"iiif-img/{id}/info.json?noOrchestrate=true");

        // Assert
        FakeImageOrchestrator.OrchestratedImages.Should().NotContain(id);
    }
    
    [Fact]
    public async Task GetInfoJson_OpenImage_ForwardedFor_Correct()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJson_OpenImage_ForwardedFor_Correct)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "iiif-img" });

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"iiif-img/{id}/info.json");
        request.Headers.Add("X-Forwarded-Host", "new-host.dlcs");
        var response = await httpClient.SendAsync(request);

        // Assert
        // TODO - improve these tests when we have IIIF models
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should()
            .Be("http://new-host.dlcs/iiif-img/99/1/GetInfoJson_OpenImage_ForwardedFor_Correct");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task GetInfoJson_RestrictedImage_Correct()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJson_RestrictedImage_Correct)}");
        const string roleName = "my-test-role";
        const string authServiceName = "my-auth-service";
        const string logoutServiceName = "my-logout-service";
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: roleName, maxUnauthorised: 500,
            deliveryChannels: new[] { "iiif-img" });
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
        infoJson.Service.Should().HaveCount(2, "Contains v1 + v2 auth services");
        infoJson.Service.First().Id.Should().Be($"http://localhost/auth/v2/probe/{id}");
        infoJson.Service.Last().Id.Should().Be("http://localhost/auth/99/test-service");
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
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJson_RestrictedImage_Correct_CustomPathRules)}");
        const string roleName = "my-test-role";
        const string authServiceName = "my-auth-service";
        const string logoutServiceName = "my-logout-service";

        await dbFixture.DbContext.Images.AddTestAsset(id, roles: roleName, maxUnauthorised: 500,
            deliveryChannels: new[] { "iiif-img" });
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
        infoJson.Service.Should().HaveCount(2, "Contains v1 + v2 auth services");
        infoJson.Service.First().Id.Should().Be($"http://my-proxy.com/proxy-probe/{id}");
        infoJson.Service.Last().Id.Should().Be("http://my-proxy.com/auth/test-service");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeFalse();
        response.Headers.CacheControl.Private.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
    }
    
    [Fact]
    public async Task GetInfoJson_RestrictedImage_NoRole_HasNoService()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJson_RestrictedImage_NoRole_HasNoService)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, maxUnauthorised: 500, deliveryChannels: new[] { "iiif-img" });

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

        infoJson.Id.Should().Be("http://localhost/iiif-img/99/1/GetInfoJson_RestrictedImage_NoRole_HasNoService");
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
        var id = AssetId.FromString(
            $"99/1/{nameof(GetInfoJson_RestrictedImage_WithUnknownRole_Returns401WithoutServices)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "unknown-role", maxUnauthorised: 500,
            deliveryChannels: new[] { "iiif-img" });

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
        var id = AssetId.FromString(
            $"99/1/{nameof(GetInfoJson_RestrictedImage_WithUnknownRole_Returns401_IfNoBearerTokenProvided)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 500,
            deliveryChannels: new[] { "iiif-img" });
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
        var id = AssetId.FromString(
            $"99/1/{nameof(GetInfoJson_RestrictedImage_WithUnknownRole_Returns401_IfUnknownBearerTokenProvided)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 500,
            deliveryChannels: new[] { "iiif-img" });
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
        var id = AssetId.FromString(
            $"99/1/{nameof(GetInfoJson_RestrictedImage_WithUnknownRole_Returns401_IfExpiredBearerTokenProvided)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 500,
            deliveryChannels: new[] { "iiif-img" });
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
        var id = AssetId.FromString(
            $"99/1/{nameof(GetInfoJson_RestrictedImage_WithUnknownRole_Returns200_AndRefreshesToken_IfValidBearerTokenProvided)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 500,
            deliveryChannels: new[] { "iiif-img" });
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

    [Fact]
    public async Task Get_UnknownCustomer_Returns404()
    {
        // Arrange
        const string path = "iiif-img/1/1/my-image/full/full/0/default.jpg";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_UnknownSpace_Returns404()
    {
        // Arrange
        const string path = "iiif-img/99/5/my-image/full/full/0/default.jpg";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task Get_UnknownImage_Returns404()
    {
        // Arrange
        const string path = "iiif-img/99/1/my-image/full/full/0/default.jpg";

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Theory]
    [InlineData("iiif-img/99/1/my-image/full/all/0/default.jpg")]
    [InlineData("iiif-img/99/1/my-image/!200,200/full/0/default.jpg")]
    public async Task Get_MalformedImageRequest_Returns400(string path)
    {
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("iiif-img/99/1/test-auth-nocookid/full/!200,200/0/default.jpg", "id")]
    [InlineData("iiif-img/test/1/test-auth-nocookdisplay/full/!200,200/0/default.jpg", "display")]
    public async Task Get_ImageRequiresAuth_Returns401_IfNoCookie(string path, string type)
    {
        // Arrange
        var id = AssetId.FromString($"99/1/test-auth-nocook{type}");
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "basic", maxUnauthorised: 100,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Theory]
    [InlineData("iiif-img/99/1/test-auth-invalidcookid/full/!200,200/0/default.jpg", "id")]
    [InlineData("iiif-img/test/1/test-auth-invalidcookdisplay/full/!200,200/0/default.jpg", "display")]
    public async Task Get_ImageRequiresAuth_Returns401_IfInvalidCookie(string path, string type)
    {
        // Arrange
        var id = AssetId.FromString($"99/1/test-auth-invalidcook{type}");
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "basic", maxUnauthorised: 100,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", "dlcs-token-99=blabla;");
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Theory]
    [InlineData("iiif-img/99/1/test-auth-expcookid/full/!200,200/0/default.jpg", "id")]
    [InlineData("iiif-img/test/1/test-auth-expcookdisplay/full/!200,200/0/default.jpg", "display")]
    public async Task Get_ImageRequiresAuth_Returns401_IfExpiredCookie(string path, string type)
    {
        // Arrange
        var id = AssetId.FromString($"99/1/test-auth-expcook{type}");
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 100,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(-1),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Theory]
    [InlineData("iiif-img/99/1/test-auth-cook-tileid/0,0,512,512/!512,512/0/default.jpg", "id")]
    [InlineData("iiif-img/test/1/test-auth-cook-tiledisplay/0,0,512,512/!512,512/0/default.jpg", "display")]
    public async Task Get_ImageRequiresAuth_RedirectsToImageServer_AndSetsCookie_IfCookieProvided_TileRequest(string path, string type)
    {
        // Arrange
        var id = AssetId.FromString($"99/1/test-auth-cook-tile{type}");
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 100,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();
        
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[400,400], [200,200]]}",
        });
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
        var response = await httpClient.SendAsync(request);
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.ToString().Should().StartWith("http://image-server/iiif");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Set-Cookie");
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Theory]
    [InlineData("iiif-img/99/1/test-auth-cookid/full/max/0/default.jpg", "id")]
    [InlineData("iiif-img/test/1/test-auth-cookdisplay/full/max/0/default.jpg", "display")]
    public async Task Get_ImageRequiresAuth_RedirectsToImageServer_AndSetsCookie_IfCookieProvided_FullRequest(string path, string type)
    {
        // Arrange
        var id = AssetId.FromString($"99/1/test-auth-cook{type}");
        await dbFixture.DbContext.Images.AddTestAsset(id, roles: "clickthrough", maxUnauthorised: 100,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();
        
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[400,400], [200,200]]}",
        });
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
        var response = await httpClient.SendAsync(request);
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.ToString().Should().StartWith("http://special-server/iiif");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Set-Cookie");
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_ImageIsExactThumbMatch_RedirectsToThumbs()
    {
        // Arrange
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = "99/1/known-thumb/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[200,200]]}",
        });

        var id = AssetId.FromString("99/1/known-thumb");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        await dbFixture.DbContext.SaveChangesAsync();
        var expectedPath = new Uri("http://thumbs/thumbs/99/1/known-thumb/full/!200,200/0/default.jpg");
        
        // Act
        var response = await httpClient.GetAsync("iiif-img/99/1/known-thumb/full/!200,200/0/default.jpg");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.Should().Be(expectedPath);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }

    [Fact]
    public async Task Get_FullRegion_LargerThumbExists_RedirectsToResizeThumbs()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_FullRegion_LargerThumbExists_RedirectsToResizeThumbs)}");
        
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[400,400], [200,200]]}",
        });
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        await dbFixture.DbContext.SaveChangesAsync();
        var expectedPath = new Uri($"http://thumbresize/thumbs/{id}/full/!123,123/0/default.jpg");
        
        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/full/!123,123/0/default.jpg");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.Should().Be(expectedPath);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_FullRegion_SmallerThumbExists_NoMatchingUpscaleConfig_RedirectsToSpecialServer()
    {
        // Arrange
        var id = AssetId.FromString(
            $"99/1/{nameof(Get_FullRegion_SmallerThumbExists_NoMatchingUpscaleConfig_RedirectsToSpecialServer)}");
        
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[400,400], [200,200]]}",
        });
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/full/!800,800/0/default.jpg");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.ToString().Should().StartWith("http://special-server/iiif");
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_FullRegion_NoOpenThumbs_RedirectsToSpecialServer()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_FullRegion_NoOpenThumbs_RedirectsToSpecialServer)}");
        
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": []}",
        });

        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/full/!800,800/0/default.jpg");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.ToString().Should().StartWith("http://special-server/iiif");
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_FullRegion_HasSmallerThumb_MatchesUpscaleRegex_ThresholdTooLarge_RedirectsToSpecialServer()
    {
        // Arrange
        var id = AssetId.FromString(
            $"99/1/upscale{nameof(Get_FullRegion_HasSmallerThumb_MatchesUpscaleRegex_ThresholdTooLarge_RedirectsToSpecialServer)}");
        
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[300,300]]}",
        });

        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/full/!800,800/0/default.jpg");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.ToString().Should().StartWith("http://special-server/iiif");
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_FullRegion_HasSmallerThumb_MatchesUpscaleRegex_WithinThreshold_RedirectsToResizeThumbs()
    {
        // Arrange
        var id = AssetId.FromString(
            $"99/1/upscale{nameof(Get_FullRegion_HasSmallerThumb_MatchesUpscaleRegex_WithinThreshold_RedirectsToResizeThumbs)}");
        
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[300,300]]}",
        });

        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        await dbFixture.DbContext.SaveChangesAsync();
        var expectedPath = new Uri($"http://thumbresize/thumbs/{id}/full/!600,600/0/default.jpg");
        
        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/full/!600,600/0/default.jpg");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.Should().Be(expectedPath);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_Thumbs_RedirectsToThumbs()
    {
        // Arrange
        var expectedPath = new Uri("http://thumbs/thumbs/99/1/known-thumb/full/!200,200/0/default.jpg");
        
        // Act
        var response = await httpClient.GetAsync("thumbs/99/1/known-thumb/full/!200,200/0/default.jpg");
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.Should().Be(expectedPath);
    }
    
    [Theory]
    [InlineData("iiif-img/99/1/resize/full/!200,200/0/default.jpg", "resize")]
    [InlineData("iiif-img/99/1/full/full/full/0/default.jpg", "full")]
    public async Task Get_RedirectsSpecialServer_AsFallThrough_ForFullRequests(string path, string imageName)
    {
        // Arrange
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"99/1/{imageName}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": []}",
        });

        var id = AssetId.FromString($"99/1/{imageName}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        await dbFixture.DbContext.CustomHeaders.AddTestCustomHeader("x-test-key", "foo bar");
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync(path);
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.ToString().Should().StartWith("http://special-server/iiif");
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.SharedMaxAge.Should().Be(TimeSpan.FromDays(28));
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromDays(28));
        response.Headers.Should().ContainKey("x-test-key").WhoseValue.Should().BeEquivalentTo("foo bar");
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Theory]
    [InlineData("iiif-img/99/1/tile/0,0,1000,1000/200,200/0/default.jpg", "tile")]
    [InlineData("iiif-img/test/1/rewrite_id/0,0,1000,1000/200,200/0/default.jpg", "rewrite_id")]
    public async Task Get_RedirectsImageServer_AsFallThrough_ForTileRequests(string path, string imageName)
    {
        // Arrange
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"99/1/{imageName}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": []}",
        });

        var id = AssetId.FromString($"99/1/{imageName}");
        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        await dbFixture.DbContext.CustomHeaders.AddTestCustomHeader("x-test-key", "foo bar");
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync(path);
        var proxyResponse = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        // Assert
        proxyResponse.Uri.ToString().Should().StartWith("http://image-server/iiif");
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.SharedMaxAge.Should().Be(TimeSpan.FromDays(28));
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromDays(28));
        response.Headers.Should().ContainKey("x-test-key").WhoseValue.Should().BeEquivalentTo("foo bar");
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_Returns404_IfRedirectsImageServer_ButOrchestratorNotFound()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_Returns404_IfRedirectsImageServer_ButOrchestratorNotFound)}");

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": []}",
        });

        FakeImageOrchestrator.ConfiguredResponse[id] = OrchestrationResult.NotFound;

        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        await dbFixture.DbContext.CustomHeaders.AddTestCustomHeader("x-test-key", "foo bar");
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/0,0,100,100/200,200/0/default.jpg");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Fact]
    public async Task Get_Returns500_IfRedirectsImageServer_ButOrchestratorError()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_Returns500_IfRedirectsImageServer_ButOrchestratorError)}");

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": []}",
        });

        FakeImageOrchestrator.ConfiguredResponse[id] = OrchestrationResult.Error;

        await dbFixture.DbContext.Images.AddTestAsset(id, origin: "/test/space", width: 1000, height: 1000,
            deliveryChannels: new[] { "iiif-img" });
        await dbFixture.DbContext.ImageLocations.AddTestImageLocation(id);
        await dbFixture.DbContext.CustomHeaders.AddTestCustomHeader("x-test-key", "foo bar");
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/0,0,100,100/200,200/0/default.jpg");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
    }
    
    [Theory]
    [InlineData("/info.json")]
    [InlineData("/full/max/0/default.jpg")]
    [InlineData("/0,0,1000,1000/200,200/0/default.jpg")]
    public async Task Get_404_IfNotForDelivery(string path)
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_404_IfNotForDelivery)}");

        // test runs 3 times so only add on first run
        if (await dbFixture.DbContext.Images.FindAsync(id) == null)
        {
            await dbFixture.DbContext.Images.AddTestAsset(id, notForDelivery: true,
                deliveryChannels: new[] { "iiif-img" });
            await dbFixture.DbContext.SaveChangesAsync();
        }

        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/{path}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Theory]
    [InlineData("/info.json")]
    [InlineData("/full/max/0/default.jpg")]
    [InlineData("/0,0,1000,1000/200,200/0/default.jpg")]
    public async Task Get_404_IfNotForImageDeliveryChannel(string path)
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(Get_404_IfNotForImageDeliveryChannel)}");

        // test runs 3 times so only add on first run
        if (await dbFixture.DbContext.Images.FindAsync(id) == null)
        {
            await dbFixture.DbContext.Images.AddTestAsset(id, deliveryChannels: new[] { "file" });
            await dbFixture.DbContext.SaveChangesAsync();
        }

        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/{path}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

public class FakeImageOrchestrator : IImageOrchestrator
{
    public static List<AssetId> OrchestratedImages { get; } = new();

    public static Dictionary<AssetId, OrchestrationResult> ConfiguredResponse { get; } = new();

    public Task<OrchestrationResult> EnsureImageOrchestrated(OrchestrationImage orchestrationImage,
        CancellationToken cancellationToken = default)
    {
        OrchestratedImages.Add(orchestrationImage.AssetId);
        var orchestrationResult = ConfiguredResponse.TryGetValue(orchestrationImage.AssetId, out var res)
            ? res
            : OrchestrationResult.Orchestrated;
        return Task.FromResult(orchestrationResult);
    }
}

public class FakeImageServerClient : IImageServerClient
{
    public async Task<TImageService> GetInfoJson<TImageService>(OrchestrationImage orchestrationImage,
        Version version,
        CancellationToken cancellationToken = default) where TImageService : JsonLdBase
    {
        if (typeof(TImageService) == typeof(ImageService2))
        {
            return new ImageService2
            {
                Profile = ImageService2.Level1Profile,
                Protocol = ImageService2.Image2Protocol,
                Context = ImageService2.Image2Context,
                Width = 100,
                Height = 100,
                Sizes = new List<Size> { new(100, 100), new(25, 25), new(1, 1) },
            } as TImageService;
        }

        return new ImageService3
        {
            Profile = ImageService3.Level1Profile,
            Protocol = ImageService3.ImageProtocol,
            Context = ImageService3.Image3Context,
            Width = 100,
            Height = 100,
            Sizes = new List<Size> { new(100, 100), new(25, 25), new(1, 1) },
        } as TImageService;
    }
}