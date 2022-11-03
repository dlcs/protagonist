using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Core.Types;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using Test.Helpers.Integration;
using Thumbs.Tests.Integration.Infrastructure;

namespace Thumbs.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class InfoJsonTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;
    private readonly IAmazonS3 amazonS3;

    public InfoJsonTests(ProtagonistAppFactory<Startup> factory, StorageFixture storageFixture)
    {
        dbFixture = storageFixture.DbFixture;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithLocalStack(storageFixture.LocalStackFixture)
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        dbFixture.CleanUp();
    }

    [Theory]
    [InlineData("/thumbs/99/1/image", "http://localhost/thumbs/99/1/image/info.json")]
    [InlineData("/thumbs/99/1/image/", "http://localhost/thumbs/99/1/image/info.json")]
    [InlineData("/thumbs/test/1/image", "http://localhost/thumbs/test/1/image/info.json")]
    [InlineData("/thumbs/test/1/image/", "http://localhost/thumbs/test/1/image/info.json")]
    [InlineData("/thumbs/v2/99/1/image", "http://localhost/thumbs/v2/99/1/image/info.json")]
    [InlineData("/thumbs/v2/99/1/image/", "http://localhost/thumbs/v2/99/1/image/info.json")]
    [InlineData("/thumbs/v2/test/1/image", "http://localhost/thumbs/v2/test/1/image/info.json")]
    [InlineData("/thumbs/v2/test/1/image/", "http://localhost/thumbs/v2/test/1/image/info.json")]
    [InlineData("/thumbs/v3/99/1/image", "http://localhost/thumbs/99/1/image/info.json")] // Canonical version goes to canonical url
    [InlineData("/thumbs/v3/99/1/image/", "http://localhost/thumbs/99/1/image/info.json")]
    [InlineData("/thumbs/v3/test/1/image", "http://localhost/thumbs/test/1/image/info.json")]
    [InlineData("/thumbs/v3/test/1/image/", "http://localhost/thumbs/test/1/image/info.json")]
    public async Task Get_ImageRoot_RedirectsToInfoJson(string path, string expected)
    {
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location.Should().Be(expected);
    }

    [Fact]
    public async Task GetInfoJsonV2_Correct_ViaDirectPath()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJsonV2_Correct_ViaDirectPath)}");
        await dbFixture.DbContext.Images.AddTestAsset(id);

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[800,800],[400,400],[200,200]]}"
        });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"thumbs/v2/{id}/info.json");

        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should()
            .Be("http://localhost/thumbs/v2/99/1/GetInfoJsonV2_Correct_ViaDirectPath");
        jsonResponse["@context"].ToString().Should().Be("http://iiif.io/api/image/2/context.json");
        jsonResponse["height"].ToString().Should().Be("800");
        jsonResponse["width"].ToString().Should().Be("800");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        await dbFixture.DbContext.Images.AddTestAsset(id);

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[800,800],[400,400],[200,200]]}"
        });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"thumbs/{id}/info.json");
        request.Headers.Add("Accept", iiif2);
        var response = await httpClient.SendAsync(request);

        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should().Be("http://localhost/thumbs/99/1/GetInfoJsonV2_Correct_ViaConneg");
        jsonResponse["@context"].ToString().Should().Be("http://iiif.io/api/image/2/context.json");
        jsonResponse["height"].ToString().Should().Be("800");
        jsonResponse["width"].ToString().Should().Be("800");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        response.Content.Headers.ContentType.ToString().Should()
            .Be("application/ld+json", "application/ld+json as Accept header specified");
    }

    [Fact]
    public async Task GetInfoJsonV3_RedirectsToCanonical_AsV3IsDefault()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJsonV3_RedirectsToCanonical_AsV3IsDefault)}");
        await dbFixture.DbContext.Images.AddTestAsset(id);

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[800,800],[400,400],[200,200]]}"
        });
        await dbFixture.DbContext.SaveChangesAsync();
        var expected = $"http://localhost/thumbs/{id}/info.json";

        // Act
        var response = await httpClient.GetAsync($"thumbs/v3/{id}/info.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().Be(expected);
    }

    [Fact]
    public async Task GetInfoJsonV3_Correct_ViaConneg()
    {
        // Arrange
        var id = AssetId.FromString($"99/1/{nameof(GetInfoJsonV3_Correct_ViaConneg)}");
        const string iiif3 = "application/ld+json; profile=\"http://iiif.io/api/image/3/context.json\"";
        await dbFixture.DbContext.Images.AddTestAsset(id);

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\": [[800,800],[400,400],[200,200]]}"
        });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"thumbs/{id}/info.json");
        request.Headers.Add("Accept", iiif3);
        var response = await httpClient.SendAsync(request);

        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be("http://localhost/thumbs/99/1/GetInfoJsonV3_Correct_ViaConneg");
        jsonResponse["@context"].ToString().Should().Be("http://iiif.io/api/image/3/context.json");
        jsonResponse["height"].ToString().Should().Be("800");
        jsonResponse["width"].ToString().Should().Be("800");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.FromSeconds(2));
        response.Content.Headers.ContentType.ToString().Should().Be(iiif3);
    }
}