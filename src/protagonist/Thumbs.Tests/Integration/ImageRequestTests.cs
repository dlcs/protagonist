using System.Net;
using System.Text.Json.Nodes;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Core.Types;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using NetVips;
using Test.Helpers.Data;
using Test.Helpers.Integration;
using Thumbs.Tests.Integration.Infrastructure;

namespace Thumbs.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class ImageRequestTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;
    private readonly IAmazonS3 amazonS3;

    public ImageRequestTests(ProtagonistAppFactory<Startup> factory, StorageFixture storageFixture)
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
    [InlineData("tif")]
    [InlineData("png")]
    [InlineData("gif")]
    [InlineData("jp2")]
    [InlineData("pdf")]
    [InlineData("webp")]
    public async Task GetThumbnail_Returns400_IfUnsupportedFormatRequested(string format)
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();

        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/full/200,150/0/default.{format}");

        // Assert
        await ValidateErrorResponse(response, id, HttpStatusCode.BadRequest,
            $"Requested format '{format}' not supported, use 'jpg'");
    }

    [Theory]
    [InlineData("bitonal")]
    [InlineData("gray")]
    public async Task GetThumbnail_Returns400_IfUnsupportedQualityRequested(string quality)
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();

        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/full/max/0/{quality}.jpg");

        // Assert
        await ValidateErrorResponse(response, id, HttpStatusCode.BadRequest,
            $"Requested quality '{quality}' not supported, use 'default' or 'color'");
    }
    
    [Theory]
    [InlineData("!0")]
    [InlineData("90")]
    public async Task GetThumbnail_Returns400_IfUnsupportedRotationRequested(string rotation)
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();

        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/full/max/{rotation}/default.jpg");

        // Assert
        await ValidateErrorResponse(response, id, HttpStatusCode.BadRequest,
            "Requested rotation value not supported, use '0'");
    }

    [Theory]
    [InlineData("full/!200,/0/default.jpg")]
    [InlineData("full/!,200/0/default.jpg")]
    [InlineData("full/!200/0/default.jpg")]
    [InlineData("info.json/full/200,/0/default.jpg")]
    public async Task GetThumbnail_Returns400_IfMalformedRequest(string imageRequest)
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();

        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/{imageRequest}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("full/200,/0/default.jpg")]
    [InlineData("full/,200/0/default.jpg")]
    [InlineData("full/!200,200/0/default.jpg")]
    [InlineData("full/200,200/0/default.jpg")]
    public async Task GetThumbnail_Returns404_IfAllAuthThumbs(string imageRequest)
    {
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id);

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\":[],\"a\": [[800,800],[400,400],[200,200]]}"
        });
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/{imageRequest}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Theory]
    [InlineData("full/200,/0/default.jpg")]
    [InlineData("full/,200/0/default.jpg")]
    [InlineData("full/!200,200/0/default.jpg")]
    [InlineData("full/200,200/0/default.jpg")]
    public async Task GetThumbnail_Returns404_IfRequestingAuthThumb_SomeOpen(string imageRequest)
    {
        // This would work if upscaling was supported but we have that disabled in all envs
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id);

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\":[[100,100]],\"a\": [[800,800],[400,400],[200,200]]}"
        });
        await dbFixture.DbContext.SaveChangesAsync();
        
        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/{imageRequest}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Theory]
    [InlineData("full/200,/0/default.jpg")]
    [InlineData("full/,200/0/default.jpg")]
    [InlineData("full/!200,200/0/default.jpg")]
    [InlineData("full/200,200/0/default.jpg")]
    public async Task GetThumbnail_Returns200_IfRequestingOpenThumb_SomeAuth(string imageRequest)
    {
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id);

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\":[[200,200]],\"a\": [[800,800],[400,400]]}"
        });
        var thumbnail = await AddThumbnail(id, 200, 200);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/{imageRequest}");

        // Assert
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should()
            .ContainSingle(a => a == id.ToString());
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        response.Headers.Should().ContainKey("x-thumb-match");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(thumbnail);
    }
    
    [Theory]
    [InlineData("full/100,/0/default.jpg")]
    [InlineData("full/,200/0/default.jpg")]
    [InlineData("full/!200,200/0/default.jpg")]
    [InlineData("full/100,200/0/default.jpg")]
    public async Task GetThumbnail_Returns200_IfThumbFound(string imageRequest)
    {
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id);

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\":[[200,400],[150,300],[100,200]],\"a\":[[500,1000],[400,800]]}"
        });
        var thumbnail = await AddThumbnail(id, 100, 200);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/{imageRequest}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should()
            .ContainSingle(a => a == id.ToString());
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        response.Headers.Should().ContainKey("x-thumb-match");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(thumbnail);
    }

    [Fact]
    public async Task GetThumbnail_Returns404_IfThumbInSizesJson_ButNotInStorage()
    {
        // Arrange - 100x200 listed as open size but no jpg exists in bucket
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id);

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\":[[100,200]],\"a\":[]}"
        });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/full/100,200/0/default.jpg");

        // Assert
        await ValidateErrorResponse(response, id, HttpStatusCode.NotFound, "Could not find requested thumbnail");
    }

    [Fact]
    public async Task GetThumbnail_Returns200_ResizedFromLargerThumb()
    {
        // Arrange - 150x300 isn't an open size so is resized from 200x400
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id);

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\":[[200,400],[100,200]],\"a\":[]}"
        });
        await AddThumbnail(id, 200, 400);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/full/150,300/0/default.jpg");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        response.Headers.Should().ContainKey("x-thumb-resized");
        using var image = Image.NewFromBuffer(await response.Content.ReadAsByteArrayAsync());
        image.Width.Should().Be(150);
        image.Height.Should().Be(300);
    }

    [Fact]
    public async Task GetThumbnail_Returns404_IfResizeSourceNotInStorage()
    {
        // Arrange - 150x300 isn't an open size, 200x400 to resize from is listed but no jpg exists in bucket
        var id = AssetIdGenerator.GetAssetId();
        await dbFixture.DbContext.Images.AddTestAsset(id);

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{id}/s.json",
            BucketName = LocalStackFixture.ThumbsBucketName,
            ContentBody = "{\"o\":[[200,400],[100,200]],\"a\":[]}"
        });
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync($"thumbs/{id}/full/150,300/0/default.jpg");

        // Assert
        await ValidateErrorResponse(response, id, HttpStatusCode.NotFound, "Could not find requested thumbnail");
    }

    /// <summary>
    /// Generate jpg of specified size and upload to thumbs bucket at open/{longestEdge}.jpg
    /// </summary>
    private async Task<byte[]> AddThumbnail(AssetId assetId, int width, int height)
    {
        using var image = Image.Black(width, height).Copy(interpretation: Enums.Interpretation.Bw);
        var jpeg = image.JpegsaveBuffer();

        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = $"{assetId}/open/{Math.Max(width, height)}.jpg",
            BucketName = LocalStackFixture.ThumbsBucketName,
            InputStream = new MemoryStream(jpeg),
            ContentType = "image/jpeg"
        });
        return jpeg;
    }

    private static async Task ValidateErrorResponse(HttpResponseMessage response, AssetId assetId,
        HttpStatusCode expectedStatusCode, string expectedMessage)
    {
        response.StatusCode.Should().Be(expectedStatusCode);
        response.Headers.Should().ContainKey("x-asset-id").WhoseValue.Should()
            .ContainSingle(a => a == assetId.ToString());
        var responseObject = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        responseObject!["message"]!.ToString().Should().Be(expectedMessage);
        responseObject["statusCode"]!.ToString().Should().Be(((int)expectedStatusCode).ToString());
    }
}
