using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using IIIF.Auth.V2;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Serialisation;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using IIIF3 = IIIF.Presentation.V3;

namespace Orchestrator.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class NamedQueryTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;

    public NamedQueryTests(ProtagonistAppFactory<Startup> factory, DlcsDatabaseFixture databaseFixture)
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
            Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "test-named-query",
            Template = "assetOrdering=n1&s1=p1&space=p2"
        });
                
        var thumbsPolicy = dbFixture.DbContext.DeliveryChannelPolicies.Single(d =>
            d.Channel == AssetDeliveryChannels.Thumbnails && d.Customer == 99);
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-1"), num1: 2, ref1: "my-ref")
            .WithTestDeliveryChannel(AssetDeliveryChannels.Thumbnails, thumbsPolicy.Id)
            .WithTestDeliveryChannel(AssetDeliveryChannels.Image);
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-2"), num1: 1, ref1: "my-ref")
            .WithTestThumbnailMetadata()
            .WithTestDeliveryChannel(AssetDeliveryChannels.Image);
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-nothumbs"), num1: 3, ref1: "my-ref",
            maxUnauthorised: 10, roles: "default")
            .WithTestThumbnailMetadata()
            .WithTestDeliveryChannel(AssetDeliveryChannels.Image);
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/not-for-delivery"), num1: 4, ref1: "my-ref",
            notForDelivery: true)
            .WithTestThumbnailMetadata()
            .WithTestDeliveryChannel(AssetDeliveryChannels.Image);

        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("100/1/auth-1"), num1: 2, ref1: "auth-ref",
            roles: "clickthrough")
            .WithTestDeliveryChannel(AssetDeliveryChannels.Image)
            .WithTestThumbnailMetadata();
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("100/1/auth-2"), num1: 1, ref1: "auth-ref",
            roles: "clickthrough")
            .WithTestDeliveryChannel(AssetDeliveryChannels.Image)
            .WithTestThumbnailMetadata();
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("100/1/no-auth"), num1: 3, ref1: "auth-ref")
            .WithTestDeliveryChannel(AssetDeliveryChannels.Image)
            .WithTestThumbnailMetadata();

        dbFixture.DbContext.SaveChanges();
    }
    
    [Theory]
    [InlineData("iiif-resource/99/unknown-nq")]
    [InlineData("iiif-resource/v2/99/unknown-nq")]
    [InlineData("iiif-resource/v3/99/unknown-nq")]
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
    [InlineData("iiif-resource/99/unknown-nq")]
    [InlineData("iiif-resource/v2/99/unknown-nq")]
    [InlineData("iiif-resource/v3/99/unknown-nq")]
    public async Task Get_Returns404_IfNQNotFound(string path)
    {
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("iiif-resource/98/test-named-query")]
    [InlineData("iiif-resource/v2/98/test-named-query")]
    [InlineData("iiif-resource/v3/98/test-named-query")]
    public async Task Get_Returns404_IfCustomerNotFound(string path)
    {
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Theory]
    [InlineData("iiif-resource/99/test-named-query/my-ref")]
    [InlineData("iiif-resource/v2/99/test-named-query/my-ref")]
    [InlineData("iiif-resource/v3/99/test-named-query/my-ref")]
    public async Task Get_Returns200_IfNamedQueryParametersLessThanMax(string path)
    {
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Theory]
    [InlineData("iiif-resource/99/test-named-query")]
    [InlineData("iiif-resource/v2/99/test-named-query")]
    [InlineData("iiif-resource/v3/99/test-named-query")]
    public async Task Get_Returns400_IfNoNamedQueryParameters(string path)
    {
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Get_Returns404_IfNoMatchingAssets()
    {
        // Act
        var response = await httpClient.GetAsync("iiif-resource/99/test-named-query/not-found-ref/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ReturnsV2ManifestWithCorrectCount_ViaConneg()
    {
        // Arrange
        const string path = "iiif-resource/99/test-named-query/my-ref/1";
        const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/2/context.json\"";
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Accept", iiif2);
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().Contain("Accept");
        response.Content.Headers.ContentType.ToString().Should().Be(iiif2);
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        var sequence = jsonResponse.SelectToken("sequences[0]");
        sequence.Value<string>("@id").Should().Contain("/iiif-query/sequence/0", "@id set for nq");
        sequence.SelectToken("canvases").Should().HaveCount(3);
    }
    
    [Fact]
    public async Task Get_ReturnsV2ManifestWithCorrectCount_ViaDirectPath()
    {
        // Arrange
        const string path = "iiif-resource/v2/99/test-named-query/my-ref/1";
        const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/2/context.json\"";
        
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().Contain("Accept");
        response.Content.Headers.ContentType.ToString().Should().Be(iiif2);
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse.SelectToken("sequences[0].canvases").Count().Should().Be(3);
    }
    
    [Fact]
    public async Task Get_ReturnsV2Manifest_WithCorrectId_IgnoringQueryParam()
    {
        // Arrange
        const string path = "iiif-resource/v2/99/test-named-query/my-ref/1";
        const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/2/context.json\"";
        
        // Act
        var response = await httpClient.GetAsync($"{path}?foo=bar");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().Contain("Accept");
        response.Content.Headers.ContentType.ToString().Should().Be(iiif2);
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["@id"].ToString().Should().Be($"http://localhost/{path}");
    }
    
    [Fact]
    public async Task Get_ReturnsV2Manifest_WithoutImageService3Services()
    {
        // Arrange
        const string path = "iiif-resource/v2/99/test-named-query/my-ref/1";
        const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/2/context.json\"";
        
        // Act
        var response = await httpClient.GetAsync($"{path}?foo=bar");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().Contain("Accept");
        response.Content.Headers.ContentType.ToString().Should().Be(iiif2);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("ImageService3");
    }
    
    [Fact]
    public async Task Get_ReturnsV3ManifestWithCorrectCount_ViaConneg()
    {
        // Arrange
        const string path = "iiif-resource/99/test-named-query/my-ref/1";
        
        const string iiif2 = "application/ld+json; profile=\"http://iiif.io/api/presentation/3/context.json\"";
        
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Accept", iiif2);
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().Contain("Accept");
        response.Content.Headers.ContentType.ToString().Should().Be(iiif2);
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse.SelectToken("items").Count().Should().Be(3);
    }
    
    [Fact]
    public async Task Get_ReturnsV3ManifestWithCorrectCount_ViaDirectPath()
    {
        // Arrange
        const string path = "iiif-resource/v3/99/test-named-query/my-ref/1";
        const string iiif3 = "application/ld+json; profile=\"http://iiif.io/api/presentation/3/context.json\"";
        
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().Contain("Accept");
        response.Content.Headers.ContentType.ToString().Should().Be(iiif3);
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse.SelectToken("items").Count().Should().Be(3);
    }
    
    [Fact]
    public async Task Get_ReturnsV3Manifest_WithCorrectId_IgnoringQueryParam()
    {
        // Arrange
        const string path = "iiif-resource/v3/99/test-named-query/my-ref/1";
        const string iiif3 = "application/ld+json; profile=\"http://iiif.io/api/presentation/3/context.json\"";
        
        // Act
        var response = await httpClient.GetAsync($"{path}?foo=bar");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().Contain("Accept");
        response.Content.Headers.ContentType.ToString().Should().Be(iiif3);
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse["id"].ToString().Should().Be($"http://localhost/{path}");
    }
    
    [Fact]
    public async Task Get_ReturnsV3ManifestWithCorrectCount_AsCanonical()
    {
        // Arrange
        const string path = "iiif-resource/99/test-named-query/my-ref/1";
        const string iiif3 = "application/ld+json; profile=\"http://iiif.io/api/presentation/3/context.json\"";
        
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().Contain("Accept");
        response.Content.Headers.ContentType.ToString().Should().Be(iiif3);
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
        jsonResponse.SelectToken("items").Should().HaveCount(3);
    }
    
    [Theory]
    [InlineData("iiif-resource/99/manifest-slash-test/with%2Fforward%2Fslashes/1")]
    [InlineData("iiif-resource/99/manifest-slash-test/with%2fforward%2fslashes/1")]
    public async Task Get_ReturnsManifestWithSlashes(string path)
    {
        // Arrange
        dbFixture.DbContext.NamedQueries.Add(new NamedQuery
        {
            Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "manifest-slash-test",
            Template = "manifest=s1&canvas=n1&s1=p1&space=p2"
        });

        await dbFixture.DbContext.Images
            .AddTestAsset(AssetId.FromString("99/1/first"), num1: 1, ref1: "with/forward/slashes")
            .WithTestDeliveryChannel("iiif-img");
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());

        jsonResponse.SelectToken("items").Should().HaveCount(1);
        jsonResponse.SelectToken("items")[0].SelectToken("id").Value<string>().Should().Contain("99/1/first");
    }
    
    [Fact]
    public async Task Get_ReturnsManifestWithCorrectlyOrderedItems()
    {
        // Arrange
        dbFixture.DbContext.NamedQueries.Add(new NamedQuery
        {
            Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "ordered-manifest",
            Template = "assetOrder=n1;n2 desc;s1&s2=p1"
        });

        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/third"), num1: 1, num2: 10, ref1: "z",
            ref2: "grace").WithTestThumbnailMetadata().WithTestDeliveryChannel(AssetDeliveryChannels.Image);
        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/first"), num1: 1, num2: 20, ref1: "c",
            ref2: "grace").WithTestThumbnailMetadata().WithTestDeliveryChannel(AssetDeliveryChannels.Image);
        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/fourth"), num1: 2, num2: 10, ref1: "a",
            ref2: "grace").WithTestThumbnailMetadata().WithTestDeliveryChannel(AssetDeliveryChannels.Image);
        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/second"), num1: 1, num2: 10, ref1: "x",
            ref2: "grace").WithTestThumbnailMetadata().WithTestDeliveryChannel(AssetDeliveryChannels.Image);
        await dbFixture.DbContext.SaveChangesAsync();

        var expectedOrder = new[] { "99/1/first", "99/1/second", "99/1/third", "99/1/fourth" };

        const string path = "iiif-resource/99/ordered-manifest/grace";
        
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());

        var count = 0;
        foreach (var token in jsonResponse.SelectToken("items"))
        {
            token["id"].Value<string>().Should().Contain(expectedOrder[count++]);
        }
    }
    
    [Fact]
    public async Task Get_AssetsRequireAuth_ReturnsV2ManifestWithoutAuthServices()
    {
        // Arrange
        const string path = "iiif-resource/v2/99/test-named-query/auth-ref/1";
        
        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonContent = await response.Content.ReadAsStringAsync();
        var jsonResponse = JObject.Parse(jsonContent);
        jsonResponse.SelectTokens("sequences[*].canvases[*].images[*].resource.service")
            .Select(token => token.ToString())
            .Should().NotContainMatch("*clickthrough*", "auth services are not included in v2 manifests");
        jsonResponse.SelectToken("sequences[0].canvases").Count().Should().Be(3);
    }
    
    [Fact]
    public async Task Get_AssetsRequireAuth_ReturnsV3ManifestWithAuthServices()
    {
        // Arrange
        const string path = "iiif-resource/v3/99/test-named-query/auth-ref/1";
        
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var manifest = (await response.Content.ReadAsStreamAsync()).FromJsonStream<IIIF3.Manifest>();
        manifest.Context.ToString().Should().Contain("http://iiif.io/api/auth/2/context.json", "Auth2 context added");
        manifest.Services.Should().ContainItemsAssignableTo<AuthAccessService2>()
            .And.HaveCount(1, "2 items require auth but they share an access service");
        
        var paintable = manifest.Items.First()
            .Items.First()
            .Items.Cast<PaintingAnnotation>().Single()
            .Body.As<IIIF.Presentation.V3.Content.Image>();
            
        paintable.Service.Should().HaveCount(3);
        paintable.Service.OfType<ImageService2>().Single().Service.Should()
            .ContainSingle(s => s is AuthProbeService2 && s.Id.Contains("auth-1"), "ImageService2 has auth service");
        paintable.Service.OfType<ImageService3>().Single().Service.Should()
            .ContainSingle(s => s is AuthProbeService2 && s.Id.Contains("auth-1"), "ImageService3 has auth service");
        paintable.Service.OfType<AuthProbeService2>().Single().Id.Should()
            .Contain("auth-1", "Image body has auth service");
    }
}
