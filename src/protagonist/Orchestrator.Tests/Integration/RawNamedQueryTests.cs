using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using DLCS.Core.Types;
using DLCS.Model.Assets.NamedQueries;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;

namespace Orchestrator.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class RawNamedQueryTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;

    public RawNamedQueryTests(ProtagonistAppFactory<Startup> factory, DlcsDatabaseFixture databaseFixture)
    {
        dbFixture = databaseFixture;
        httpClient = factory
            .WithTestServices(services =>
            {
                services.AddSingleton<IIIIFAuthBuilder, FakeAuth2Client>();
            })
            .WithConnectionString(dbFixture.ConnectionString)
            .CreateClient();

        dbFixture.CleanUp();

        // Setup a basic NQ for testing
        dbFixture.DbContext.NamedQueries.Add(new NamedQuery
        {
            Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "test-raw-named-query",
            Template = "assetOrdering=n1&s1=p1&space=p2"
        });
        
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-1"), num1: 2, ref1: "my-ref");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-2"), num1: 1, ref1: "my-ref");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-nothumbs"), num1: 3, ref1: "my-ref",
            maxUnauthorised: 10, roles: "default");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/not-for-delivery"), num1: 4, ref1: "my-ref",
            notForDelivery: true);
        
        dbFixture.DbContext.SaveChanges();
    }
    
    [Theory]
    [InlineData("raw-resource/99/unknown-nq")]
    [InlineData("raw-resource/test/unknown-nq")]
    public async Task Options_Returns200_WithCorsHeaders(string path)
    {
        // Act
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
        response.Headers.Should().ContainKey("Access-Control-Allow-Headers");
        response.Headers.Should().ContainKey("Access-Control-Allow-Methods");
    }

    [Theory]
    [InlineData("raw-resource/99/unknown-nq")]
    [InlineData("raw-resource/test/unknown-nq")]
    public async Task Get_Returns404_IfNQNotFound(string path)
    {
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("raw-resource/98/test-raw-named-query")]
    [InlineData("raw-resource/foo/test-raw-named-query")]
    public async Task Get_Returns404_IfCustomerNotFound(string path)
    {
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Theory]
    [InlineData("raw-resource/99/test-raw-named-query/my-ref")]
    [InlineData("raw-resource/test/test-raw-named-query/my-ref")]
    public async Task Get_Returns200_IfNamedQueryParametersLessThanMax(string path)
    {
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Theory]
    [InlineData("raw-resource/99/test-raw-named-query")]
    [InlineData("raw-resource/test/test-raw-named-query")]
    public async Task Get_Returns400_IfNoNamedQueryParameters(string path)
    {
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("raw-resource/99/test-raw-named-query/not-found-ref/1")]
    [InlineData("raw-resource/test/test-raw-named-query/not-found-ref/1")]
    public async Task Get_Returns404_IfNoMatchingAssets(string path)
    {
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("raw-resource/99/test-raw-named-query/my-ref/1")]
    [InlineData("raw-resource/test/test-raw-named-query/my-ref/1")]
    public async Task Get_ReturnsCorrectList(string path)
    {
        // Arrange
        var expectedMatches = new List<string>
        {
            "99/1/matching-1", "99/1/matching-2", "99/1/matching-nothumbs"
        };
        
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = JsonConvert.DeserializeObject<List<string>>(await response.Content.ReadAsStringAsync());
        results.Should().BeEquivalentTo(expectedMatches);
    }
    
    [Fact]
    public async Task Get_ReturnsManifestWithCorrectlyOrderedItems()
    {
        // Arrange
        dbFixture.DbContext.NamedQueries.Add(new NamedQuery
        {
            Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "raw-ordered-manifest",
            Template = "assetOrder=n1;n2 desc;s1&s2=p1"
        });

        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/third"), num1: 1, num2: 10, ref1: "z",
            ref2: "grace").WithTestThumbnailMetadata();;
        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/first"), num1: 1, num2: 20, ref1: "c",
            ref2: "grace").WithTestThumbnailMetadata();;
        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/fourth"), num1: 2, num2: 10, ref1: "a",
            ref2: "grace").WithTestThumbnailMetadata();;
        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/second"), num1: 1, num2: 10, ref1: "x",
            ref2: "grace").WithTestThumbnailMetadata();;
        await dbFixture.DbContext.SaveChangesAsync();

        var expectedOrder = new[] { "99/1/first", "99/1/second", "99/1/third", "99/1/fourth" };

        const string path = "raw-resource/99/raw-ordered-manifest/grace";
        
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        var results = JsonConvert.DeserializeObject<List<string>>(await response.Content.ReadAsStringAsync());

        results.Should().BeEquivalentTo(expectedOrder);
    }
}