using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using IIIF.ImageApi.V3;
using IIIF.Serialisation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Orchestrator.Features.Images.ImageServer;
using Orchestrator.Features.Images.Orchestration;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using Yarp.ReverseProxy.Forwarder;

namespace Orchestrator.Tests.Integration;

/// <summary>
/// Test of info.json requests that are dependant on OldestAllowedInfoJson property
/// </summary>
[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class RefreshInfoJsonHandlingTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;
    private readonly IAmazonS3 amazonS3;
    private readonly FakeImageOrchestrator orchestrator = new();
    private const string SizesJsonContent = "{\"o\":[[800,800],[400,400],[200,200]],\"a\":[]}";

    public RefreshInfoJsonHandlingTests(ProtagonistAppFactory<Startup> factory, StorageFixture storageFixture)
    {
        dbFixture = storageFixture.DbFixture;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithConfigValue("OldestAllowedInfoJson", DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd")) // tomorrow
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
    
    [Fact]
    public async Task GetInfoJson_Refreshed_IfAlreadyInS3_ButOutOfDate()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJson_Refreshed_IfAlreadyInS3_ButOutOfDate)}");
        await dbFixture.DbContext.Images.AddTestAsset(id, imageDeliveryChannels: new List<ImageDeliveryChannel>
        {
            new()
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicyId = 1
            }
        });
        await dbFixture.DbContext.SaveChangesAsync();

        var s3StorageKey = $"{id}/info/Cantaloupe/v3/info.json";
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = SizesJsonContent
        });
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = s3StorageKey,
            BucketName = LocalStackFixture.StorageBucketName,
            ContentBody = "{\"@context\": \"_this_proves_s3_origin_\"}"
        });

        // Act
        var response = await httpClient.GetAsync($"iiif-img/{id}/info.json");
        
        // Assert
        // Verify correct info.json returned
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be($"http://localhost/iiif-img/{id}");
        jsonResponse["@context"].ToString().Should()
            .NotBe("_this_proves_s3_origin_", "infojson created before OldestAllowedInfoJson");

        // With correct headers/status
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should().ContainSingle(id.ToString());
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        response.Content.Headers.ContentType.ToString().Should()
            .Be("application/ld+json; profile=\"http://iiif.io/api/image/3/context.json\"");
        
        // Verify new info.json written
        var newInfoJson = await amazonS3.GetObjectAsync(new GetObjectRequest
        {
            Key = s3StorageKey,
            BucketName = LocalStackFixture.StorageBucketName,
        });
        var imageService3 = newInfoJson.ResponseStream.FromJsonStream<ImageService3>();
        imageService3.Profile.Should().Be(ImageService3.Level1Profile, "Profile set from image-server response");
    }
}
