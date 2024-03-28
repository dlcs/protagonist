using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DLCS.Core.Types;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
public class ModifyAssetWithoutOldDeliveryChannelEmulationTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    
    public ModifyAssetWithoutOldDeliveryChannelEmulationTests(
        ProtagonistAppFactory<Startup> factory)
    {
        httpClient = factory
            .WithTestServices(services =>
            {
                services.AddAuthentication("API-Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "API-Test", _ => { });
            })
            .WithConfigValue("DeliveryChannelsEnabled", "true")
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }
    
    [Fact]
    public async Task Put_Asset_Fails_When_ThumbnailPolicy_Is_Provided()
    {
        // Arrange 
        var assetId = new AssetId(99, 1, $"{nameof(Put_Asset_Fails_When_ThumbnailPolicy_Is_Provided)}");
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""mediaType"":""image/tiff"",
          ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
          ""family"": ""I"",
          ""wcDeliveryChannels"": [
            ""iiif-img""
            ],
          ""thumbnailPolicy"": ""thumbs-policy""
        }}";    
        
        // Act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ImageOptimisationPolicy and ThumbnailPolicy are disabled");
    }
    
    [Fact]
    public async Task Put_Asset_Fails_When_ImageOptimisationPolicy_Is_Provided()
    {
        // Arrange 
        var assetId = new AssetId(99, 1, $"{nameof(Put_Asset_Fails_When_ThumbnailPolicy_Is_Provided)}");
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""mediaType"":""image/tiff"",
          ""origin"": ""https://example.org/{assetId.Asset}.tiff"",
          ""family"": ""I"",
          ""wcDeliveryChannels"": [
            ""iiif-img""
            ],
          ""imageOptimisationPolicy"": ""image-optimisation-policy""
        }}";    
        
        // Act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PutAsync(assetId.ToApiResourcePath(), content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ImageOptimisationPolicy and ThumbnailPolicy are disabled");
    }
}