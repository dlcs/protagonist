using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using API.Tests.Integration.Infrastructure;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using DLCS.Repository;
using DLCS.Repository.Messaging;
using FakeItEasy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class CustomerQueueWithOldDeliveryChannelPropertiesTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;
    private static readonly IEngineClient EngineClient = A.Fake<IEngineClient>();

    public CustomerQueueWithOldDeliveryChannelPropertiesTests(DlcsDatabaseFixture dbFixture,
        ProtagonistAppFactory<Startup> factory)
    {
        dbContext = dbFixture.DbContext;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
            {
                services.AddScoped<IEngineClient>(_ => EngineClient);
                services.AddAuthentication("API-Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "API-Test", _ => { });
            })
            .WithConfigValue("EmulateOldDeliveryChannelProperties", "true")
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }

    [Fact]
    public async Task Post_CreateBatch_201_IfImageOptimisationPolicySetForImage_AndLegacyEnabled()
    {
        const int customerId = 325665;
        const int space = 201;
        await dbContext.Customers.AddTestCustomer(customerId);
        await dbContext.Spaces.AddTestSpace(customerId, space);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customerId);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customerId);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customerId);
        await dbContext.SaveChangesAsync();

        // Arrange
        var hydraImageBody = @"{
            ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
            ""@type"": ""Collection"",
            ""member"": [
                {
                  ""@id"": ""https://test/customers/325665/spaces/201/images/one"",
                  ""origin"": ""https://example.org/my-image.png"",
                  ""imageOptimisationPolicy"": ""fast-higher"",
                  ""space"": 201,
                },
            ]
        }";

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = $"/customers/{customerId}/queue";

        // Act
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
  
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var image = dbContext.Images
            .Include(a => a.ImageDeliveryChannels!)
            .ThenInclude(dc => dc.DeliveryChannelPolicy)
            .Single(i => i.Customer == customerId && i.Space == space);
        
        image.ImageDeliveryChannels!.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Image && 
                  dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageDefault,
            dc => dc.Channel == AssetDeliveryChannels.Thumbnails && 
                  dc.DeliveryChannelPolicy.Name == "default");
    }
    
    [Fact]
    public async Task Post_CreateBatch_201_IfThumbnailPolicySetForImage_AndLegacyEnabled()
    {
        const int customerId = 325665;
        const int space = 201;
        await dbContext.Customers.AddTestCustomer(customerId);
        await dbContext.Spaces.AddTestSpace(customerId, space);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customerId);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customerId);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customerId);
        await dbContext.SaveChangesAsync();

        // Arrange
        var hydraImageBody = @"{
            ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
            ""@type"": ""Collection"",
            ""member"": [
                {
                  ""@id"": ""https://test/customers/325665/spaces/201/images/one"",
                  ""origin"": ""https://example.org/my-image.png"",
                  ""thumbnailPolicy"": ""default"",
                  ""space"": 201,
                },
            ]
        }";

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = $"/customers/{customerId}/queue";

        // Act
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
  
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var image = dbContext.Images
            .Include(a => a.ImageDeliveryChannels!)
            .ThenInclude(dc => dc.DeliveryChannelPolicy)
            .Single(i => i.Customer == customerId && i.Space == space);
        
        image.ImageDeliveryChannels!.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Image && 
                  dc.DeliveryChannelPolicyId == KnownDeliveryChannelPolicies.ImageDefault,
            dc => dc.Channel == AssetDeliveryChannels.Thumbnails && 
                  dc.DeliveryChannelPolicy.Name == "default");
    }
    
    [Fact]
    public async Task Post_CreateBatch_201_IfImageOptimisationPolicySetForVideo_AndLegacyEnabled()
    {
        const int customerId = 325665;
        const int space = 201;
        await dbContext.Customers.AddTestCustomer(customerId);
        await dbContext.Spaces.AddTestSpace(customerId, space);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customerId);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customerId);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customerId);
        await dbContext.SaveChangesAsync();

        // Arrange
        var hydraImageBody = @"{
            ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
            ""@type"": ""Collection"",
            ""member"": [
                {
                  ""@id"": ""https://test/customers/325665/spaces/201/images/one"",
                  ""family"": ""T"",
                  ""origin"": ""https://example.org/my-video.mp4"",
                  ""imageOptimisationPolicy"": ""video-max"",
                  ""space"": 201,
                },
            ]
        }";

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = $"/customers/{customerId}/queue";

        // Act
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
  
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var image = dbContext.Images
            .Include(a => a.ImageDeliveryChannels!)
            .ThenInclude(dc => dc.DeliveryChannelPolicy)
            .Single(i => i.Customer == customerId && i.Space == space);

        image.ImageDeliveryChannels!.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Timebased &&
                  dc.DeliveryChannelPolicy.Name == "default-video");
    }
    
    [Fact]
    public async Task Post_CreateBatch_201_IfImageOptimisationPolicySetForAudio_AndLegacyEnabled()
    {
        const int customerId = 325665;
        const int space = 201;
        await dbContext.Customers.AddTestCustomer(customerId);
        await dbContext.Spaces.AddTestSpace(customerId, space);
        await dbContext.DeliveryChannelPolicies.AddTestDeliveryChannelPolicies(customerId);
        await dbContext.DefaultDeliveryChannels.AddTestDefaultDeliveryChannels(customerId);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customerId);
        await dbContext.SaveChangesAsync();

        // Arrange
        var hydraImageBody = @"{
            ""@context"": ""http://www.w3.org/ns/hydra/context.jsonld"",
            ""@type"": ""Collection"",
            ""member"": [
                {
                  ""@id"": ""https://test/customers/325665/spaces/201/images/one"",
                  ""family"": ""T"",
                  ""origin"": ""https://example.org/my-audio.mp3"",
                  ""imageOptimisationPolicy"": ""audio-max"",
                  ""space"": 201,
                },
            ]
        }";

        var content = new StringContent(hydraImageBody, Encoding.UTF8, "application/json");
        var path = $"/customers/{customerId}/queue";

        // Act
        var response = await httpClient.AsCustomer(customerId).PostAsync(path, content);
  
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var image = dbContext.Images
            .Include(a => a.ImageDeliveryChannels!)
            .ThenInclude(dc => dc.DeliveryChannelPolicy)
            .Single(i => i.Customer == customerId && i.Space == space);

        image.ImageDeliveryChannels!.Should().Satisfy(
            dc => dc.Channel == AssetDeliveryChannels.Timebased &&
                  dc.DeliveryChannelPolicy.Name == "default-audio");
    }
}