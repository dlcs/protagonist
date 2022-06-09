using System.Net;
using Amazon.S3;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
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
            .CreateClient(new WebApplicationFactoryClientOptions {AllowAutoRedirect = false});
            
        dbFixture.CleanUp();
    }

    [Theory]
    [InlineData("/thumbs/99/1/image")]
    [InlineData("/thumbs/99/1/image/")]
    [InlineData("/thumbs/test/1/image")]
    [InlineData("/thumbs/test/1/image/")]
    [InlineData("/thumbs/v2/99/1/image")]
    [InlineData("/thumbs/v2/99/1/image/")]
    [InlineData("/thumbs/v2/test/1/image")]
    [InlineData("/thumbs/v2/test/1/image/")]
    [InlineData("/thumbs/v3/99/1/image")]
    [InlineData("/thumbs/v3/99/1/image/")]
    [InlineData("/thumbs/v3/test/1/image")]
    [InlineData("/thumbs/v3/test/1/image/")]
    public async Task Get_ImageRoot_RedirectsToInfoJson(string path)
    {
        // Arrange
        var expected = path[^1] == '/' ? $"http://localhost{path}info.json" : $"http://localhost{path}/info.json";
        
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location.Should().Be(expected);
    }
}