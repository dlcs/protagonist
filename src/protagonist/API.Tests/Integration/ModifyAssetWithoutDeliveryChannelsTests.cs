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
public class ModifyAssetWithoutDeliveryChannelsTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly HttpClient httpClient;
    
    public ModifyAssetWithoutDeliveryChannelsTests(
        ProtagonistAppFactory<Startup> factory)
    {
        httpClient = factory
            .WithTestServices(services =>
            {
                services.AddAuthentication("API-Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "API-Test", _ => { });
            })
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }
    
    [Fact]
    public async Task Patch_Asset_Fails_When_Delivery_Channels_Are_Disabled()
    {
        // Arrange 
        var assetId = new AssetId(99, 1, $"{nameof(Patch_Asset_Fails_When_Delivery_Channels_Are_Disabled)}");
        var hydraImageBody = $@"{{
          ""@type"": ""Image"",
          ""string1"": ""I am edited"",
          ""wcDeliveryChannels"": [
                ""iiif-img""
            ]
        }}";    
        
        // Act
        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var response = await httpClient.AsCustomer(99).PatchAsync(assetId.ToApiResourcePath(), content);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Delivery channels are disabled");
    }
}